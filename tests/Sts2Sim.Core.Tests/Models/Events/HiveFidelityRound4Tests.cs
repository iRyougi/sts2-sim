using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Tests.Models.Cards;
using Sts2Sim.Core.ValueProps;
using PaelsLegion = Sts2Sim.Core.Models.Relics.PaelsLegion;

namespace Sts2Sim.Core.Tests.Models.Events;

file sealed class HiveRound4SelectionSource(
    Func<CardSelectionRequest, IReadOnlyList<CardModel>> select) : IRunDecisionSource
{
    public List<CardSelectionRequest> Requests { get; } = new();

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options[0]);

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
    {
        Requests.Add(request);
        return Task.FromResult(select(request));
    }
}

[Collection("ModelDb")]
public sealed class HiveFidelityRound4Tests : IDisposable
{
    public HiveFidelityRound4Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(
            [typeof(NormalKeywordTestCard), typeof(InnateKeywordTestCard)]));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task PaelsLegion_FinalZeroBlockDoesNotRecordCardPlayOrStartCooldown()
    {
        (RunState run, Player player) = CreateRun("legion-final-zero");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsLegion>(), player);
        CombatRoom room = await EnterCombat(run);
        ClearCombatPiles(player);
        NoBlockPower noBlock = Assert.IsType<NoBlockPower>(await PowerCmd.Apply<NoBlockPower>(
            room.Engine.State, player.Creature, 2m, player.Creature, null));

        await room.Engine.PlayCardAsync(player, AddToHand<DefendRegent>(player), null);
        Assert.Equal(0, player.Creature.Block);

        await PowerCmd.Remove(noBlock);
        await room.Engine.PlayCardAsync(player, AddToHand<DefendRegent>(player), null);

        Assert.Equal(10, player.Creature.Block);
    }

    [Fact]
    public async Task InitialShuffle_ImbuedInnateStaysAtBottomWhileOrdinaryInnateMovesToTop()
    {
        (RunState _, Player player) = CreateRun("imbued-innate-order");
        ClearDeck(player);
        BigBang imbuedInnate = AddToDeck<BigBang>(player);
        CardCmd.Upgrade(imbuedInnate);
        await CardCmd.Enchant<Imbued>(imbuedInnate, 1m);
        InnateKeywordTestCard ordinaryInnate = AddToDeck<InnateKeywordTestCard>(player);
        for (int i = 0; i < 8; i++)
            _ = AddToDeck<NormalKeywordTestCard>(player);

        player.ResetCombatState();
        player.PopulateCombatState(new Rng(7uL));

        IReadOnlyList<CardModel> drawPile = player.PlayerCombatState!.DrawPile.Cards;
        CardModel combatImbuedInnate = Assert.Single(drawPile,
            card => card is BigBang && card.Enchantments.OfType<Imbued>().Any());
        Assert.True(combatImbuedInnate.HasKeyword(CardKeyword.Innate));
        Assert.Same(combatImbuedInnate, drawPile[^1]);
        Assert.IsType<InnateKeywordTestCard>(drawPile[0]);
        Assert.NotSame(combatImbuedInnate, drawPile[0]);
        Assert.NotSame(ordinaryInnate, drawPile[0]);
    }

    [Fact]
    public async Task ElectricShrymp_UsesRunDecisionSourceToEnchantNonFirstEligibleCard()
    {
        (RunState run, Player player) = CreateRun("electric-non-first");
        var source = new HiveRound4SelectionSource(request => [request.Candidates[1]]);
        _ = new RunDriver(run, source);
        CardModel[] eligible = player.Deck.Cards
            .Where(ModelDb.GetById<Imbued>(ModelDb.GetId<Imbued>()).CanEnchant)
            .ToArray();

        await RelicCmd.Obtain(ModelDb.Relic<ElectricShrymp>(), player);

        CardSelectionRequest request = Assert.Single(source.Requests);
        Assert.Equal(1, request.MinCount);
        Assert.Equal(1, request.MaxCount);
        Assert.IsType<ElectricShrymp>(request.Source);
        Assert.Equal(eligible, request.Candidates);
        Assert.Empty(eligible[0].Enchantments);
        Assert.IsType<Imbued>(Assert.Single(eligible[1].Enchantments));
    }

    [Fact]
    public async Task PaelsTooth_UsesRunDecisionSourceToRemoveFiveNonFirstCandidates()
    {
        (RunState run, Player player) = CreateRun("tooth-non-first");
        var source = new HiveRound4SelectionSource(request => request.Candidates.Skip(1).Take(5).ToArray());
        _ = new RunDriver(run, source);
        CardModel[] candidates = player.Deck.Cards
            .Where(card => card.IsRemovable && card.IsUpgradable)
            .ToArray();
        CardModel[] expected = candidates.Skip(1).Take(5).ToArray();

        await RelicCmd.Obtain(ModelDb.Relic<PaelsTooth>(), player);

        CardSelectionRequest request = Assert.Single(source.Requests);
        Assert.Equal(5, request.MinCount);
        Assert.Equal(5, request.MaxCount);
        Assert.IsType<PaelsTooth>(request.Source);
        Assert.Equal(candidates, request.Candidates);
        Assert.Contains(candidates[0], player.Deck.Cards);
        Assert.DoesNotContain(player.Deck.Cards, card => expected.Contains(card));
        Assert.Equal(5, player.Deck.Cards.Count);
    }

    [Fact]
    public async Task NonCombatSelection_RejectsCardOutsideOfferedCandidates()
    {
        (RunState run, Player player) = CreateRun("non-combat-illegal");
        CardModel outside = (CardModel)ModelDb.Card<Alignment>().MutableClone();
        outside.AssignOwner(player);
        var source = new HiveRound4SelectionSource(_ => [outside]);
        _ = new RunDriver(run, source);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RelicCmd.Obtain(ModelDb.Relic<ElectricShrymp>(), player));

        Assert.Contains("outside", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static (RunState Run, Player Player) CreateRun(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }

    private static async Task<CombatRoom> EnterCombat(RunState run)
    {
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        return room;
    }

    private static T AddToHand<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static T AddToDeck<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        player.Deck.AddInternal(card);
        return card;
    }

    private static void ClearDeck(Player player)
    {
        foreach (CardModel card in player.Deck.Cards.ToList())
            player.Deck.RemoveInternal(card);
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToList())
            CardPileCmd.Remove(card);
    }
}
