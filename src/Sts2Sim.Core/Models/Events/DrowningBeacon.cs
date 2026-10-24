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

    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("BOTTLE", [VisibleValue.Int("PotionCount", 1), VisibleValue.Id("Potion", "GLOWWATER_POTION")]),
        new("CLIMB", [VisibleValue.Int("RelicCount", 1), VisibleValue.Int("HpLoss", 13), VisibleValue.Id("Relic", "FRESNEL_LENS"), VisibleValue.Scope("LossKind", "MaxHp")])
    ]);

    protected override VisibleOption DescribeVisibleOption(EventOption option, int displayIndex, VisibleOption declared) =>
        option.Key switch
        {
            "BOTTLE" => declared with { Contents = [VisibleContentProjection.ForPotion(ModelDb.Potion<GlowwaterPotion>())] },
            "CLIMB" => declared with { Contents = [VisibleContentProjection.ForRelic(ModelDb.Relic<FresnelLens>())] },
            _ => base.DescribeVisibleOption(option, displayIndex, declared),
        };

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
