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

    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("COMBINE_STRIKES", [VisibleValue.Int("RemoveCount", 2), VisibleValue.Scope("RemovedTag", "Strike"), VisibleValue.Int("ObtainCount", 1), VisibleValue.Id("Card1", "ULTIMATE_STRIKE")]),
        new("COMBINE_DEFENDS", [VisibleValue.Int("RemoveCount", 2), VisibleValue.Scope("RemovedTag", "Defend"), VisibleValue.Int("ObtainCount", 1), VisibleValue.Id("Card2", "ULTIMATE_DEFEND")])
    ]);

    protected override VisibleOption DescribeVisibleOption(EventOption option, int displayIndex, VisibleOption declared) =>
        option.Key switch
        {
            "COMBINE_STRIKES" => declared with { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<UltimateStrike>())] },
            "COMBINE_DEFENDS" => declared with { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<UltimateDefend>())] },
            _ => base.DescribeVisibleOption(option, displayIndex, declared),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("COMBINE_STRIKES", () => CombineAsync(CardTag.Strike, typeof(UltimateStrike))),
        new EventOption("COMBINE_DEFENDS", () => CombineAsync(CardTag.Defend, typeof(UltimateDefend))),
    };

    /// <summary>原版 <c>CombineStrikes</c> / <c>CombineDefends</c>：玩家用
    /// <c>CardSelectCmd.FromDeckForRemoval</c> 选 2 张（过滤 <see cref="IsValid"/>），按选择顺序移除。</summary>
    private async Task CombineAsync(CardTag tag, Type resultType)
    {
        foreach (CardModel card in (await CardSelectCmd.FromDeckForRemoval(
                     Owner, 2, this, card => IsValid(tag, card))).ToList())
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
