using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Commands;

public sealed class ReplaceNewRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public int ObtainedCount { get; private set; }

    public override Task AfterObtained()
    {
        ObtainedCount++;
        return Task.CompletedTask;
    }
}
