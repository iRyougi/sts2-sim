using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>NecroMasteryPower</c>：持有者的 Osty 每失去生命，对所有可命中的敌人造成
/// 失去量 × 层数的不可格挡、不受加成伤害（来源为持有者）。</summary>
public sealed class NecroMasteryPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (delta >= 0m || creature.Monster is not Osty || creature.PetOwner != Owner.Player)
            return;

        await CreatureCmd.Damage(creature.CombatState!, creature.CombatState!.HittableEnemies,
            -delta * Amount, ValueProp.Unblockable | ValueProp.Unpowered, Owner, null, null);
    }
}
