using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Commands;

[Collection("ModelDb")]
public sealed class CardCmdTransformToTests : IDisposable
{
    public CardCmdTransformToTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight),
            typeof(Greed),
            typeof(Peck),
            typeof(AstralPulse),
            typeof(WanderingGrunt),
            typeof(TransformGenerationProbeRelic),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task TransformTo_NullSource_ThrowsBeforeAnyMutation()
    {
        (RunState runState, Player player) = Setup("transform-to-null");
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CardCmd.TransformTo<Peck>(null!, runState));

        Assert.Equal(deckBefore, player.Deck.Cards);
    }

    [Fact]
    public async Task TransformTo_NullRunState_ThrowsBeforeAnyMutation()
    {
        (_, Player player) = Setup("transform-to-null-run");
        CardModel source = player.Deck.Cards[0];
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CardCmd.TransformTo<Peck>(source, null!));

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Same(player.Deck, source.Pile);
    }
    [Fact]
    public async Task TransformTo_CardFromAnotherRun_ThrowsBeforeAnyMutation()
    {
        (_, Player player) = Setup("transform-to-owner-run");
        var otherRun = new RunState("transform-to-other-run", new Overgrowth());
        CardModel source = player.Deck.Cards[0];
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformTo<Peck>(source, otherRun));

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Same(player.Deck, source.Pile);
    }
    [Fact]
    public async Task TransformTo_CardWithoutPile_ThrowsBeforeAnyMutation()
    {
        (RunState runState, Player player) = Setup("transform-to-no-pile");
        var source = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        source.AssignOwner(player);
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformTo<Peck>(source, runState));

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Null(source.Pile);
    }
    [Fact]
    public async Task TransformTo_NonTransformableCard_IsRejectedBeforeTargetLookupOrMutation()
    {
        (RunState runState, Player player) = Setup("transform-to-eternal");
        var source = (Greed)ModelDb.Card<Greed>().MutableClone();
        source.AssignOwner(player);
        CardPileCmd.Add(source, PileType.Deck);
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformTo<Infection>(source, runState));

        Assert.Contains("Eternal", exception.Message);
        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Same(player.Deck, source.Pile);
    }
    [Fact]
    public async Task TransformTo_MissingTargetCanonical_ThrowsBeforeAnyMutation()
    {
        (RunState runState, Player player) = Setup("transform-to-missing-target");
        CardModel source = player.Deck.Cards[0];
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformTo<Infection>(source, runState));

        Assert.Contains("registered", exception.Message);
        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Same(player.Deck, source.Pile);
    }
    [Theory]
    [InlineData("deck")]
    [InlineData("combat-typed")]
    [InlineData("combat-direct")]
    [InlineData("ended-deck")]
    public async Task TransformTo_ReplacesSourceWithOwnedTargetInTheSamePile(string scenario)
    {
        (RunState runState, Player player) = Setup("transform-to-success");
        await RelicCmd.Obtain(ModelDb.Relic<TransformGenerationProbeRelic>(), player);
        TransformGenerationProbeRelic probe = player.Relics.OfType<TransformGenerationProbeRelic>().Single();
        if (scenario != "deck")
        {
            var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
            await room.Enter(runState);
            if (scenario == "ended-deck")
            {
                await CreatureCmd.SetCurrentHp(room.Engine.State.HittableEnemies.Single(), 0m);
                Assert.True(room.Engine.CheckWinCondition());
                Assert.False(room.Engine.State.IsLiveCombat());
                Assert.True(room.Engine.State.IsOverOrEnding());
            }
        }
        bool inCombatPile = scenario.StartsWith("combat-", StringComparison.Ordinal);
        CardModel source = inCombatPile ? player.PlayerCombatState!.Hand.Cards[0] : player.Deck.Cards[0];
        CardPile sourcePile = source.Pile!;

        Peck target;
        if (scenario == "combat-direct")
        {
            target = (Peck)ModelDb.Card<Peck>().MutableClone();
            await CardCmd.Transform(source, target);
        }
        else
        {
            target = await CardCmd.TransformTo<Peck>(source, runState);
        }

        Assert.IsType<Peck>(target);
        Assert.Same(player, target.Owner);
        Assert.Same(sourcePile, target.Pile);
        Assert.Contains(target, sourcePile.Cards);
        Assert.DoesNotContain(sourcePile.Cards, card => ReferenceEquals(card, source));
        Assert.Null(source.Pile);
        if (inCombatPile)
        {
            var call = Assert.Single(probe.Calls);
            Assert.Same(target, call.Card);
            Assert.Same(player, call.Creator);
        }
        else
        {
            Assert.Empty(probe.Calls);
        }
    }

    [Theory]
    [InlineData("direct", false)]
    [InlineData("direct", true)]
    [InlineData("typed", false)]
    [InlineData("typed", true)]
    [InlineData("random", false)]
    [InlineData("random", true)]
    [InlineData("batch", false)]
    [InlineData("batch", true)]
    public async Task Transform_WhenCombatEnding_DoesNotMutateOrConsumeRng(string entry, bool playerLoses)
    {
        (RunState runState, Player player) = Setup($"transform-ending-{entry}-{playerLoses}");
        await RelicCmd.Obtain(ModelDb.Relic<TransformGenerationProbeRelic>(), player);
        TransformGenerationProbeRelic probe = player.Relics.OfType<TransformGenerationProbeRelic>().Single();
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        var original = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        original.AssignOwner(player);
        CardPileCmd.Add(original, PileType.Hand);
        Assert.Contains(CardFactory.GetDefaultTransformationOptions(original, isInCombat: true),
            candidate => candidate is AstralPulse);
        Dictionary<CardPile, CardModel[]> pilesBefore = player.PlayerCombatState!.AllPiles
            .ToDictionary(pile => pile, pile => pile.Cards.ToArray());
        int generatedBefore = player.PlayerCombatState.CardsGeneratedThisCombat;
        var rng = new Rng(235UL);
        int counterBefore = rng.Counter;
        await CreatureCmd.SetCurrentHp(playerLoses ? player.Creature : room.Engine.State.HittableEnemies.Single(), 0m);
        Assert.True(room.Engine.State.IsLiveCombat());
        Assert.True(room.Engine.State.IsOverOrEnding());

        switch (entry)
        {
            case "direct":
            {
                var replacement = (Peck)ModelDb.Card<Peck>().MutableClone();
                await CardCmd.Transform(original, replacement);
                Assert.Null(replacement.Pile);
                break;
            }
            case "typed":
            {
                Peck replacement = await CardCmd.TransformTo<Peck>(original, runState);
                Assert.Null(replacement.Pile);
                break;
            }
            case "random":
                await Assert.ThrowsAsync<InvalidOperationException>(() => CardCmd.TransformToRandom(original, rng, runState));
                break;
            case "batch":
                Assert.Empty(await CardCmd.Transform([new CardTransformation(original)], rng));
                break;
        }

        foreach ((CardPile pile, CardModel[] cards) in pilesBefore) Assert.Equal(cards, pile.Cards);
        Assert.Same(player.PlayerCombatState.Hand, original.Pile);
        Assert.Equal(generatedBefore, player.PlayerCombatState.CardsGeneratedThisCombat);
        Assert.Empty(probe.Calls);
        Assert.Equal(counterBefore, rng.Counter);
    }

    private static (RunState RunState, Player Player) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}

internal sealed class TransformGenerationProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public List<(CardModel Card, Player? Creator)> Calls { get; } = new();
    public IReadOnlyList<CardModel> ExpectedBatchCards { get; set; } = Array.Empty<CardModel>();

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        foreach (CardModel expected in ExpectedBatchCards)
        {
            Assert.NotNull(expected.Pile);
            Assert.Contains(expected, expected.Pile.Cards);
        }
        Calls.Add((card, creator));
        return Task.CompletedTask;
    }
}
