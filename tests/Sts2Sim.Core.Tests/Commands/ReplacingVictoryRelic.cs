using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Commands;

public sealed class ReplacingVictoryRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task AfterCombatVictory() => RelicCmd.Replace(
        this,
        (VictoryCountingRelic)ModelDb.Relic<VictoryCountingRelic>().MutableClone());
}
