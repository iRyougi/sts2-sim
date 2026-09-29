using Sts2Sim.Core.Events;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Events;

public sealed class HungryForMushrooms : EventModel
{
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
