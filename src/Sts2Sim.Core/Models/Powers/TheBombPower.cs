using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class TheBombPower : PowerModel
{
    private decimal _damage = 40m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public void SetDamage(decimal damage)
    {
        AssertMutable();
        _damage = damage;
    }

    public override async Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        if (Amount > 1)
        {
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
            return;
        }

        await CreatureCmd.Damage(
            Owner.CombatState!,
            Owner.CombatState!.HittableEnemies,
            _damage,
            ValueProp.Unpowered,
            Owner,
            null,
            null);
        await PowerCmd.Remove(this);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_damage);
    }
}
