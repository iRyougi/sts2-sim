using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class WarHistorianRepy : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("UNLOCK_CAGE", [VisibleValue.Id("ConsumedCard", ModelDb.GetId<LanternKey>().Entry), VisibleValue.Id("Relic", ModelDb.GetId<HistoryCourse>().Entry), VisibleValue.Int("RelicCount", 1)])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<LanternKey>()), VisibleContentProjection.ForRelic(ModelDb.Relic<HistoryCourse>())] },
        new("UNLOCK_CHEST", [VisibleValue.Id("ConsumedCard", ModelDb.GetId<LanternKey>().Entry), VisibleValue.Int("PotionCount", 2), VisibleValue.Int("RelicCount", 2), VisibleValue.Condition("Potion", "Random"), VisibleValue.Condition("Relic", "Random")])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<LanternKey>())] },
    ]);

    public override bool CanAppearNaturally => false;
    public override bool IsAllowed(IRunState runState) => false;
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("UNLOCK_CAGE", () => Unlock(true, false)), new("UNLOCK_CHEST", () => Unlock(false, false))];

    private async Task Unlock(bool cage, bool second)
    {
        var keys = Owner.Deck.Cards.OfType<LanternKey>().ToArray();
        foreach (var key in second || RunState.Players.Count > 1 ? keys : keys.Take(1))
        {
            PlayerCmd.CompleteQuest(key);
            await CardPileCmd.RemoveFromDeck(Owner, key);
        }
        if (cage)
        {
            if (RunState is RunState run) run.FreedRepy = true;
            await RelicCmd.Obtain(ModelDb.Relic<HistoryCourse>(), Owner);
        }
        else
        {
            Reward[] rewards = [CreatePotionReward(), CreatePotionReward(), new RelicReward(Owner), new RelicReward(Owner)];
            foreach (var reward in rewards) reward.Populate(RunState);
            OfferRewards(RewardsSet.CreateCustom(Owner, extraRewards: rewards));
        }
        if (!second && RunState.Players.Count <= 1 && Owner.Deck.Cards.Any(c => c is LanternKey))
            SetOptions([new(cage ? "UNLOCK_CHEST" : "UNLOCK_CAGE", () => Unlock(!cage, true))]);
        else Finish();
    }

    private PotionReward CreatePotionReward() => new(
        PotionFactory.CreateRandomOutOfCombat(Owner, Owner.PlayerRng.Rewards)
            ?? throw new InvalidOperationException("No eligible potion is available for the Repy chest."), Owner);
}
