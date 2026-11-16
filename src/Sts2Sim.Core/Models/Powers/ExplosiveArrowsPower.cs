using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>箭矢穿刺目标时对全部可命中敌人造成伤害（原版 <c>ExplosiveArrowsPower</c>）。</summary>
public sealed class ExplosiveArrowsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterArrowImpaled(ImpaledPower power, ArrowSlot arrow, CardModel? source)
    {
        if (!ReferenceEquals(power.Arrows.FirstOrDefault()?.Owner.Creature, Owner) || Owner.CombatState is null)
            return;

        Creature[] enemies = Owner.CombatState.Enemies.Where(c => c.IsHittable).ToArray();
        if (enemies.Length > 0)
        {
            await CreatureCmd.Damage(Owner.CombatState, enemies, Amount, ValueProp.Unpowered, Owner, null, null);
        }
    }
}
