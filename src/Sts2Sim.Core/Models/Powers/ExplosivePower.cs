using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ExplosivePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDeath(Creature target, CardModel? source, bool wasRemovalPrevented)
    {
        if (target != Owner || wasRemovalPrevented)
        {
            return;
        }

        ICombatState combatState = Owner.CombatState!;
        Creature[] targets = combatState.PlayerCreatures.Where(c => c.IsAlive).ToArray();
        await CreatureCmd.Damage(
            combatState,
            targets,
            Amount,
            ValueProp.Unpowered | ValueProp.SkipHurtAnim,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }
}
