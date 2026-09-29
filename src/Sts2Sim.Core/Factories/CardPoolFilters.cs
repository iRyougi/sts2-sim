using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Factories;

/// <summary>Canonical eligibility filters for card pools shared by rewards, merchants, and effects.</summary>
public static class CardPoolFilters
{
    // v0.111.0 EventCardPool.GenerateAllCards, excluding the ten cards whose
    // VisualCardPool is explicitly overridden to a character pool. Event and Token
    // pools are visually colorless even when CardModel.IsColorless is false.
    private static readonly HashSet<Type> VisuallyColorlessEventCards =
    [
        typeof(Abundance), typeof(Apotheosis), typeof(Apparition), typeof(BrightestFlame),
        typeof(ByrdSwoop), typeof(Enlightenment), typeof(Exterminate), typeof(FeedingFrenzy),
        typeof(MadScience), typeof(Maul), typeof(Metamorphosis), typeof(NeowsFury),
        typeof(Peck), typeof(Relax), typeof(Squash), typeof(ToricToughness),
        typeof(Wish), typeof(Whistle),
    ];

    public static bool IsVisuallyColorless(CardModel card) =>
        card.IsColorless || card.Rarity == CardRarity.Token ||
        VisuallyColorlessEventCards.Contains(card.GetType());

    public static IEnumerable<CardModel> ForSinglePlayer(IEnumerable<CardModel> cards) =>
        cards.Where(card => !card.IsMultiplayerOnly);

    public static IEnumerable<CardModel> ForCombatGeneration(IEnumerable<CardModel> cards) =>
        ForCombatGeneration(cards, isMultiplayer: false);

    public static IEnumerable<CardModel> ForCombatGeneration(
        IEnumerable<CardModel> cards,
        bool isMultiplayer) =>
        CardPoolProjection.FilterCombatCandidates(cards, isMultiplayer);
}
