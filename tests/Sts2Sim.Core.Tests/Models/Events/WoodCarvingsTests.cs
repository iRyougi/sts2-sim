using Sts2Sim.Core.Combat;
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
    public async Task Bird_TransformsTheChosenBasicTransformableCardIntoPeck()
    {
        CardModel? nonBasic = null;
        (_, Player player, WoodCarvings ev) = Setup(
            "wood-bird",
            owner => nonBasic = AddToDeckTop<AstralPulse>(owner));
        CardModel[] candidates = player.Deck.Cards.Where(IsBasicTransformable).ToArray();

        await Choose(ev, "BIRD");

        CardModel source = AssertSingleChoice(ev, candidates, "WoodCarvings.Bird → CardSelectCmd.FromDeckGeneric");
        Assert.Same(nonBasic, player.Deck.Cards[0]);
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, source));
        Assert.All(candidates.SkipLast(1), card => Assert.Contains(card, player.Deck.Cards));
        Assert.Contains(player.Deck.Cards, card => card is Peck);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Torus_TransformsTheChosenBasicTransformableCardIntoToricToughness()
    {
        CardModel? nonBasic = null;
        (_, Player player, WoodCarvings ev) = Setup(
            "wood-torus",
            owner => nonBasic = AddToDeckTop<AstralPulse>(owner));
        CardModel[] candidates = player.Deck.Cards.Where(IsBasicTransformable).ToArray();

        await Choose(ev, "TORUS");

        CardModel source = AssertSingleChoice(ev, candidates, "WoodCarvings.Torus → CardSelectCmd.FromDeckGeneric");
        Assert.Same(nonBasic, player.Deck.Cards[0]);
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, source));
        Assert.All(candidates.SkipLast(1), card => Assert.Contains(card, player.Deck.Cards));
        Assert.Contains(player.Deck.Cards, card => card is ToricToughness);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Snake_EnchantsTheChosenSlitherCandidateWithMagnitudeOne()
    {
        (_, Player player, WoodCarvings ev) = Setup(
            "wood-snake",
            owner => AddToDeckTop<Greed>(owner));
        Slither slither = ModelDb.GetById<Slither>(ModelDb.GetId<Slither>());
        CardModel[] candidates = player.Deck.Cards.Where(slither.CanEnchant).ToArray();

        await Choose(ev, "SNAKE");

        CardModel expected = AssertSingleChoice(ev, candidates, "WoodCarvings.Snake → CardSelectCmd.FromDeckForEnchantment");
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

    private readonly PickLastSource _selector = new();

    private static bool IsBasicTransformable(CardModel card) =>
        card.IsTransformable && card.Rarity == CardRarity.Basic;

    /// <summary>The game asks the player for one card (min = max = 1, not cancelable) from the filtered deck in deck order;
    /// the selector picks the last candidate so a first-card shortcut cannot pass.</summary>
    private CardModel AssertSingleChoice(WoodCarvings ev, CardModel[] expectedCandidates, string gameCall)
    {
        Assert.True(expectedCandidates.Length > 1, $"{gameCall}: fixture needs a non-trivial choice");
        CardSelectionRequest request = Assert.Single(_selector.Requests);
        Assert.Same(ev, request.Source);
        Assert.Equal(1, request.MinCount);
        Assert.Equal(1, request.MaxCount);
        Assert.False(request.Cancelable, gameCall);
        Assert.True(expectedCandidates.SequenceEqual(request.Candidates), $"{gameCall}: candidates are the filtered deck in deck order");
        return expectedCandidates[^1];
    }

    private (RunState RunState, Player Player, WoodCarvings Event) Setup(
        string seed,
        Action<Player>? configureDeck = null)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(_selector);
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

    private sealed class PickLastSource : ICardSelectionDecisionSource
    {
        public List<CardSelectionRequest> Requests { get; } = new();

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Requests.Add(request);
            return Task.FromResult<IReadOnlyList<CardModel>>(new[] { request.Candidates[^1] });
        }
    }
}
