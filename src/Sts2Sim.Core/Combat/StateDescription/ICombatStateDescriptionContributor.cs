using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat.StateDescription;

internal interface ICombatStateDescriptionContributor
{
    void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context);
}
