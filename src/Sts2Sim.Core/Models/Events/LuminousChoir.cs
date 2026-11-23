using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 event offering a two-card sacrifice or a random-rarity relic purchase.
/// 偏离 #148：不移植 DynamicVars，费用保存在私有字段；#150 的 IsAllowed predicate 已在此实现，事件池选择由 Task 3 集成。</summary>
public sealed class LuminousChoir : EventModel
{
    private const decimal BaseTributeCost = 149m;

    private decimal _cost;

    // Native also has a locked tribute row. Keep the existing simulator option shape unchanged.
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("REACH_INTO_THE_FLESH", [VisibleValue.Int("RemoveCount", 2), VisibleValue.Id("Curse", "SPORE_MIND"),
            VisibleValue.Int("CurseCount", 1)])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<SporeMind>())] },
        new("OFFER_TRIBUTE", [VisibleValue.Int("Gold", (int)_cost), VisibleValue.Int("RelicCount", 1),
            VisibleValue.Condition("Relic", "Random")]),
        new("OFFER_TRIBUTE_LOCKED", [VisibleValue.Int("Gold", (int)_cost)]),
    ]);

    protected override void CalculateVars() => _cost = 149m - Rng.NextInt(0, 50);
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Gold >= BaseTributeCost &&
                                       player.RelicGrabBag.HasAvailableRelics());


    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>
        {
            new("REACH_INTO_THE_FLESH", ReachIntoTheFleshAsync),
        };
        if (Owner.Gold >= _cost && Owner.RelicGrabBag.HasAvailableRelics())
        {
            options.Add(new EventOption("OFFER_TRIBUTE", OfferTributeAsync));
        }

        return options.AsReadOnly();
    }

    private async Task ReachIntoTheFleshAsync()
    {
        foreach (CardModel card in (await CardSelectCmd.FromDeckForRemoval(Owner, 2, this)))
        {
            await CardPileCmd.RemoveFromDeck(Owner, card);
        }

        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<SporeMind>() }, Owner);
        Finish();
    }

    private async Task OfferTributeAsync()
    {
        await PlayerCmd.LoseGold(_cost, Owner, GoldLossType.Spent);
        RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner);
        await RelicCmd.Obtain(relic, Owner);
        Finish();
    }
}
