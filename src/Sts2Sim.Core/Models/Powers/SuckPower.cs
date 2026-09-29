namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class SuckPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterAttack(AttackCommand command)
    {
        if (command.Attacker != Owner || command.TargetSide == Owner.Side ||
            !command.DamageProps.IsPoweredAttack())
        {
            return;
        }

        int hitGroups = command.Results.Count(results => results.Any(result => result.UnblockedDamage > 0));
        if (hitGroups > 0)
        {
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, Amount * hitGroups, Owner, null);
        }
    }
}
