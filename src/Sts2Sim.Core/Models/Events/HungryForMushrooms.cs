using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Events;

public sealed class HungryForMushrooms : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("BIG_MUSHROOM", [VisibleValue.Id("Relic", "BIG_MUSHROOM"), VisibleValue.Int("RelicCount", 1),
            VisibleValue.Int("MaxHp", 20), VisibleValue.Int("Cards", 2), VisibleValue.Scope("DrawReductionScope", "FirstTurn")])
        { Contents = [VisibleContentProjection.ForRelic(ModelDb.Relic<BigMushroom>())] },
        new("FRAGRANT_MUSHROOM", [VisibleValue.Id("Relic", "FRAGRANT_MUSHROOM"), VisibleValue.Int("RelicCount", 1),
            VisibleValue.Int("HpLoss", 15), VisibleValue.Int("UpgradeCount", 2), VisibleValue.Condition("UpgradeTargets", "Random")])
        { Contents = [VisibleContentProjection.ForRelic(ModelDb.Relic<FragrantMushroom>())] },
    ]);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("BIG_MUSHROOM", BigMushroomAsync),
        new EventOption("FRAGRANT_MUSHROOM", FragrantMushroomAsync),
    ];

    private async Task BigMushroomAsync()
    {
        await RelicCmd.Obtain(ModelDb.Relic<BigMushroom>(), Owner);
        Finish();
    }

    private async Task FragrantMushroomAsync()
    {
        await RelicCmd.Obtain(ModelDb.Relic<FragrantMushroom>(), Owner);
        Finish();
    }
}
