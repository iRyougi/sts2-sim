using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Commands;

public sealed class VictoryCountingRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public int VictoryCount { get; private set; }

    public override Task AfterCombatVictory()
    {
        VictoryCount++;
        return Task.CompletedTask;
    }
}
