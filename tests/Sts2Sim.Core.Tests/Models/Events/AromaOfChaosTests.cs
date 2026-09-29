using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class AromaOfChaosTests : IDisposable
{
    public AromaOfChaosTests()
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
            typeof(AstralPulse),
            typeof(Begone),
            typeof(Greed),
            typeof(AromaOfChaos),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task DeckSelectors_UseConfiguredPolicy_AndHandleNoCandidates()
    {
        (_, Player player, _) = Setup("aroma-selectors");
        CardModel first = player.Deck.Cards[0];

        IReadOnlyList<CardModel> transformations =
            await CardSelectCmd.FromDeckForTransformation(player, 2, ModelDb.Event<AromaOfChaos>());
        IReadOnlyList<CardModel> upgrades = await CardSelectCmd.FromDeckForUpgrade(player, 1, ModelDb.Event<AromaOfChaos>());

        Assert.Equal(player.Deck.Cards.Take(2), transformations);
        Assert.Same(first, Assert.Single(upgrades));

        foreach (CardModel card in player.Deck.Cards.Where(card => card.IsUpgradable))
        {
            CardCmd.Upgrade(card);
        }

        Assert.Empty(await CardSelectCmd.FromDeckForUpgrade(player, 1, ModelDb.Event<AromaOfChaos>()));
    }

    [Fact]
    public async Task TransformToRandom_ReplacesTheCardInItsPile_AndExcludesTheOriginalCardId()
    {
        (RunState runState, Player player, _) = Setup("aroma-transform-command");
        var original = (AstralPulse)ModelDb.Card<AstralPulse>().MutableClone();
        original.AssignOwner(player);
        CardPileCmd.Add(original, PileType.Deck);

        CardModel replacement = await CardCmd.TransformToRandom(
            original,
            new Rng(123UL),
            runState);

        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, original));
        Assert.Contains(replacement, player.Deck.Cards);
        Assert.IsType<Begone>(replacement);
        Assert.Same(player, replacement.Owner);
    }

    [Fact]
    public async Task TransformToRandom_WithoutAPile_ThrowsBeforeAdvancingRngOrMutatingTheCard()
    {
        (RunState runState, Player player, _) = Setup("aroma-transform-no-pile");
        var original = (AstralPulse)ModelDb.Card<AstralPulse>().MutableClone();
        original.AssignOwner(player);
        var rng = new Rng(456UL);
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformToRandom(original, rng, runState));

        Assert.Equal(0, rng.Counter);
        Assert.Null(original.Pile);
        Assert.Equal(deckBefore, player.Deck.Cards);
    }

    [Fact]
    public async Task TransformToRandom_EternalDeckCard_ThrowsBeforeAdvancingRngOrMutatingThePile()
    {
        (RunState runState, Player player, _) = Setup("aroma-transform-eternal");
        var original = (Greed)ModelDb.Card<Greed>().MutableClone();
        original.AssignOwner(player);
        CardPileCmd.Add(original, PileType.Deck);
        var rng = new Rng(789UL);
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformToRandom(original, rng, runState));

        Assert.Equal(0, rng.Counter);
        Assert.Same(original, player.Deck.Cards.Single(card => ReferenceEquals(card, original)));
        Assert.Equal(deckBefore, player.Deck.Cards);
    }

    [Fact]
    public async Task LetGo_TransformsTheFirstEligibleDeckCard()
    {
        (_, Player player, AromaOfChaos ev) = Setup("aroma-let-go");
        CardModel original = player.Deck.Cards[0];

        await Choose(ev, "LET_GO");

        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, original));
        Assert.Equal(10, player.Deck.Cards.Count);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task MaintainControl_UpgradesTheFirstEligibleDeckCard()
    {
        (_, Player player, AromaOfChaos ev) = Setup("aroma-maintain");
        CardModel expected = player.Deck.Cards.First(card => card.IsUpgradable);

        await Choose(ev, "MAINTAIN_CONTROL");

        Assert.True(expected.IsUpgraded);
        Assert.All(player.Deck.Cards.Skip(1), card => Assert.False(card.IsUpgraded));
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task MaintainControl_WithNoUpgradableCards_FinishesWithoutChangingTheDeck()
    {
        (_, Player player, AromaOfChaos ev) = Setup("aroma-maintain-empty");
        foreach (CardModel card in player.Deck.Cards.Where(card => card.IsUpgradable))
        {
            CardCmd.Upgrade(card);
        }
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Choose(ev, "MAINTAIN_CONTROL");

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, AromaOfChaos Event) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        var ev = (AromaOfChaos)ModelDb.Event<AromaOfChaos>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(AromaOfChaos ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
