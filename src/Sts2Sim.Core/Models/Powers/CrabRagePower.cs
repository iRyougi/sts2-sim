namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class CrabRagePower : PowerModel
{
    public override Task AfterDeath(Creature target, bool wasRemovalPrevented) => AfterDeath(target);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override async Task AfterDeath(Creature target)
    {
        if (target == Owner || target.Side != Owner.Side || Owner.IsDead || !Owner.Powers.Contains(this)) return;
        await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, 6m, Owner, null);
        await CreatureCmd.GainBlock(Owner.CombatState!, Owner, 99m, ValueProp.Unpowered, null, null);
        await PowerCmd.Remove(this);
    }
}
