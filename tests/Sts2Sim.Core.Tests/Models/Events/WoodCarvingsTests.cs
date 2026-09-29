using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class WoodCarvingsTests : IDisposable
{
    public WoodCarvingsTests()
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
            typeof(Greed),
            typeof(Peck),
            typeof(ToricToughness),
            typeof(Slither),
            typeof(WoodCarvings),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();


    [Fact]
    public void IsAllowed_RequiresARemovableBasicCardInTheDeck()
    {
        var runState = new RunState("wood-is-allowed", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        WoodCarvings carvings = ModelDb.Event<WoodCarvings>();

        Assert.True(carvings.IsAllowed(runState));

        foreach (CardModel card in player.Deck.Cards
                     .Where(card => card.Rarity == CardRarity.Basic && card.IsRemovable)
                     .ToList())
        {
            CardPileCmd.Remove(card);
        }

        Assert.False(carvings.IsAllowed(runState));
    }
    [Fact]
    public async Task InitialOptions_ShowSnakeOnlyWhenTheDeckHasASlitherCandidate()
    {
        (_, _, WoodCarvings eligible) = Setup("wood-snake-eligible");
        (_, _, WoodCarvings locked) = Setup(
            "wood-snake-locked",
            player =>
            {
                foreach (CardModel card in player.Deck.Cards.ToList())
                {
                    CardPileCmd.Remove(card);
                }
                AddToDeckTop<Greed>(player);
            });

        Assert.Equal(new[] { "BIRD", "SNAKE", "TORUS" }, eligible.CurrentOptions.Select(option => option.Key));
        Assert.Equal(new[] { "BIRD", "TORUS" }, locked.CurrentOptions.Select(option => option.Key));

        await Choose(locked, "BIRD");
        Assert.True(locked.IsFinished);
    }

    [Fact]
    public async Task Bird_TransformsTheFirstBasicTransformableCardIntoPeck()
    {
        CardModel? nonBasic = null;
        (_, Player player, WoodCarvings ev) = Setup(
            "wood-bird",
            owner => nonBasic = AddToDeckTop<AstralPulse>(owner));
        CardModel source = player.Deck.Cards.First(card => card.Rarity == CardRarity.Basic && card.IsTransformable);

        await Choose(ev, "BIRD");

        Assert.Same(nonBasic, player.Deck.Cards[0]);
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, source));
        Assert.Contains(player.Deck.Cards, card => card is Peck);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Torus_TransformsTheFirstBasicTransformableCardIntoToricToughness()
    {
        CardModel? nonBasic = null;
        (_, Player player, WoodCarvings ev) = Setup(
            "wood-torus",
            owner => nonBasic = AddToDeckTop<AstralPulse>(owner));
        CardModel source = player.Deck.Cards.First(card => card.Rarity == CardRarity.Basic && card.IsTransformable);

        await Choose(ev, "TORUS");

        Assert.Same(nonBasic, player.Deck.Cards[0]);
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, source));
        Assert.Contains(player.Deck.Cards, card => card is ToricToughness);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Snake_EnchantsTheFirstSlitherCandidateWithMagnitudeOne()
    {
        (_, Player player, WoodCarvings ev) = Setup(
            "wood-snake",
            owner => AddToDeckTop<Greed>(owner));
        Slither slither = ModelDb.GetById<Slither>(ModelDb.GetId<Slither>());
        CardModel expected = player.Deck.Cards.First(slither.CanEnchant);

        await Choose(ev, "SNAKE");

        var attached = Assert.IsType<Slither>(Assert.Single(expected.Enchantments));
        Assert.Equal(1m, attached.Magnitude);
        Assert.All(
            player.Deck.Cards.Where(card => !ReferenceEquals(card, expected)),
            card => Assert.Empty(card.Enchantments));
        Assert.True(ev.IsFinished);
    }

    [Theory]
    [InlineData("BIRD")]
    [InlineData("TORUS")]
    public async Task TransformOption_WithNoBasicCandidate_FinishesWithoutChangingDeck(string key)
    {
        (_, Player player, WoodCarvings ev) = Setup(
            $"wood-{key}-no-basic",
            owner =>
            {
                foreach (CardModel card in owner.Deck.Cards.ToList())
                {
                    CardPileCmd.Remove(card);
                }
                AddToDeckTop<AstralPulse>(owner);
            });
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Choose(ev, key);

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, WoodCarvings Event) Setup(
        string seed,
        Action<Player>? configureDeck = null)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        configureDeck?.Invoke(player);
        var ev = (WoodCarvings)ModelDb.Event<WoodCarvings>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static TCard AddToDeckTop<TCard>(Player owner) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(owner);
        CardPileCmd.Add(card, PileType.Deck, CardPilePosition.Top);
        return card;
    }

    private static Task Choose(WoodCarvings ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
