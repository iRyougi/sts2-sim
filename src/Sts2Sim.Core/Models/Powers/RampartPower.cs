namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

public sealed class RampartPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        ICombatState combatState = Owner.CombatState!;
        if (side != CombatSide.Player || (combatState as CombatState)?.IsPlayerExtraTurn == true)
        {
            return;
        }

        foreach (Creature turret in combatState.Enemies
                     .Where(creature => creature.IsAlive && creature.Monster is TurretOperator))
        {
            await CreatureCmd.GainBlock(
                combatState,
                turret,
                Amount,
                ValueProp.Unpowered,
                null,
                null);
        }
    }
}
