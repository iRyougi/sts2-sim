using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Commands;

public sealed class ReplaceOldRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public int RemovedCount { get; private set; }

    public override Task AfterRemoved()
    {
        RemovedCount++;
        return Task.CompletedTask;
    }
}
