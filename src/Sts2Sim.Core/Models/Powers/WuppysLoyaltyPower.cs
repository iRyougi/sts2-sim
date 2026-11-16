using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>v0.113.1 <c>WuppysLoyaltyPower</c>：主人受到的有威力攻击在格挡前改由存活的 Wuppy 承受
/// （<c>ModifyPreBlockDamageTarget</c>，#920 留的接入点）。死去的 Wuppy 留在战斗中且不能被命中，能力不随死亡移除。
/// 标题里的 HasSeenOsty 只是本地化文本，省略。</summary>
public sealed class WuppysLoyaltyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Creature? ModifyPreBlockDamageTarget(Creature originalTarget, Creature? target,
        decimal amount, ValueProp props, Creature? dealer)
    {
        if (originalTarget != Owner.PetOwner?.Creature) return target;
        if (Owner.IsDead) return target;
        if (!props.IsPoweredAttack()) return target;
        return Owner;
    }

    // 原版对所有生物都这样判断，不只针对持有者。
    public override bool ShouldAllowHitting(Creature creature) => creature.IsAlive;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) => creature != Owner;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}
