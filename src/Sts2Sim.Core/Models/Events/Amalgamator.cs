using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class Amalgamator : EventModel
{
    public override bool IsAllowed(IRunState runState) => runState.Players.All(player =>
        player.Deck.Cards.Count(card => IsValid(CardTag.Strike, card)) >= 2 &&
        player.Deck.Cards.Count(card => IsValid(CardTag.Defend, card)) >= 2);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("COMBINE_STRIKES", () => CombineAsync(CardTag.Strike, typeof(UltimateStrike))),
        new EventOption("COMBINE_DEFENDS", () => CombineAsync(CardTag.Defend, typeof(UltimateDefend))),
    };

    private async Task CombineAsync(CardTag tag, Type resultType)
    {
        foreach (CardModel card in Owner.Deck.Cards.Where(card => IsValid(tag, card)).Take(2).ToList())
        {
            await CardPileCmd.RemoveFromDeck(Owner, card);
        }

        var result = (CardModel)ModelDb.Get(resultType).MutableClone();
        result.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(result);
        Finish();
    }

    private static bool IsValid(CardTag tag, CardModel card) =>
        card.Tags.Contains(tag) && card.Rarity == CardRarity.Basic && card.IsRemovable;
}
