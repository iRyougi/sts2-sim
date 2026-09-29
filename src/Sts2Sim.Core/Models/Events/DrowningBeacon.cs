using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class DrowningBeacon : EventModel
{
    private const decimal MaxHpLoss = 13m;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new("BOTTLE", Bottle),
        new("CLIMB", Climb),
    ];

    private Task Bottle()
    {
        OfferRewards(RewardsSet.CreateCustom(
            Owner,
            extraRewards: [new PotionReward((PotionModel)ModelDb.Potion<GlowwaterPotion>().MutableClone(), Owner)]));
        Finish();
        return Task.CompletedTask;
    }

    private async Task Climb()
    {
        await CreatureCmd.LoseMaxHp(RunState, Owner.Creature, MaxHpLoss, isFromCard: false);
        await RelicCmd.Obtain(ModelDb.Relic<FresnelLens>(), Owner);
        Finish();
    }
}
