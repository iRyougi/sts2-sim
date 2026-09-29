namespace Sts2Sim.Core.Tests.Commands;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class CardCmdDiscardTests : IDisposable
{
    public CardCmdDiscardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar),
            typeof(Venerate), typeof(DivineRight), typeof(DiscardProbeRelic),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Discard_BatchMovesCardsAndFiresHookOncePerCardInOrder()
    {
        (Player player, CombatState combatState) = CreateCombat("discard-batch");
        var probe = (DiscardProbeRelic)ModelDb.Relic<DiscardProbeRelic>().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        StrikeRegent first = AddToPile<StrikeRegent>(player, PileType.Hand);
        DefendRegent second = AddToPile<DefendRegent>(player, PileType.Hand);

        await CardCmd.Discard(new CardModel[] { first, second });

        Assert.Empty(player.PlayerCombatState!.Hand.Cards);
        Assert.Equal(new CardModel[] { first, second }, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(new CardModel[] { first, second }, probe.DiscardedCards);
        Assert.Equal(new[] { 1, 0 }, probe.HandCardsAtDiscardHook.Select(hand => hand.Length));
        Assert.Equal(new[] { 1, 2 }, probe.DiscardPileCountsAtDiscardHook);
        Assert.Same(combatState, first.CombatState);
    }

    [Fact]
    public async Task DiscardAndDraw_MovesWholeBatchAndDrawsBeforeOrderedDiscardHooks()
    {
        (Player player, _) = CreateCombat("discard-draw-order");
        var probe = (DiscardProbeRelic)ModelDb.Relic<DiscardProbeRelic>().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        StrikeRegent first = AddToPile<StrikeRegent>(player, PileType.Hand);
        DefendRegent second = AddToPile<DefendRegent>(player, PileType.Hand);
        FallingStar drawn = AddToPile<FallingStar>(player, PileType.Draw);

        await CardCmd.DiscardAndDraw(new CardModel[] { first, second }, cardsToDraw: 1);

        Assert.Equal(new CardModel[] { first, second }, probe.DiscardedCards);
        Assert.All(probe.HandCardsAtDiscardHook, hand =>
            Assert.Contains(hand, card => ReferenceEquals(card, drawn)));
        Assert.All(probe.DiscardPileCountsAtDiscardHook, count => Assert.Equal(2, count));
    }

    [Fact]
    public async Task Discard_SingleUsesSameMovementAndHookPath()
    {
        (Player player, _) = CreateCombat("discard-single");
        var probe = (DiscardProbeRelic)ModelDb.Relic<DiscardProbeRelic>().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        StrikeRegent card = AddToPile<StrikeRegent>(player, PileType.Hand);

        await CardCmd.Discard(card);

        Assert.Same(card, Assert.Single(player.PlayerCombatState!.DiscardPile.Cards));
        Assert.Same(card, Assert.Single(probe.DiscardedCards));
    }

    [Fact]
    public async Task FromHandForDiscard_OffersOnlyHandCardsAndRequiresRequestedCount()
    {
        (Player player, CombatState combatState) = CreateCombat("discard-select");
        StrikeRegent first = AddToPile<StrikeRegent>(player, PileType.Hand);
        DefendRegent selected = AddToPile<DefendRegent>(player, PileType.Hand);
        AddToPile<FallingStar>(player, PileType.Draw);
        var selectionSource = new RecordingSelectionSource(request => new[] { request.Candidates[1] });
        combatState.CardSelectionSource = selectionSource;

        IReadOnlyList<CardModel> result = await CardSelectCmd.FromHandForDiscard(
            combatState,
            player,
            count: 1,
            source: null);

        CardSelectionRequest request = Assert.Single(selectionSource.Requests);
        Assert.Equal(new CardModel[] { first, selected }, request.Candidates);
        Assert.Equal((1, 1), (request.MinCount, request.MaxCount));
        Assert.Same(selected, Assert.Single(result));
    }

    private static (Player Player, CombatState CombatState) CreateCombat(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        return (player, combatState);
    }

    private static TCard AddToPile<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType);
        return card;
    }

    private sealed class RecordingSelectionSource(
        Func<CardSelectionRequest, IEnumerable<CardModel>> select) : ICardSelectionDecisionSource
    {
        public List<CardSelectionRequest> Requests { get; } = new();

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Requests.Add(request);
            return Task.FromResult<IReadOnlyList<CardModel>>(select(request).ToList());
        }
    }
}

file sealed class DiscardProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.None;

    public List<CardModel> DiscardedCards { get; private set; } = new();

    public List<CardModel[]> HandCardsAtDiscardHook { get; private set; } = new();

    public List<int> DiscardPileCountsAtDiscardHook { get; private set; } = new();

    public override Task AfterCardDiscarded(CardModel card)
    {
        DiscardedCards.Add(card);
        HandCardsAtDiscardHook.Add(card.Owner.PlayerCombatState!.Hand.Cards.ToArray());
        DiscardPileCountsAtDiscardHook.Add(card.Owner.PlayerCombatState.DiscardPile.Cards.Count);
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        DiscardedCards = new List<CardModel>();
        HandCardsAtDiscardHook = new List<CardModel[]>();
        DiscardPileCountsAtDiscardHook = new List<int>();
    }
}
