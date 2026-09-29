using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Osty 身上的能力：主人受到的有威力攻击，未格挡部分改由存活的 Osty 承受。
/// 同时让死去的 Osty 留在战斗里、不能被命中，能力本身在 Osty 死后保留，复活时不必重新施加。</summary>
public sealed class DieForYouPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal amount, ValueProp props,
        Creature? dealer)
    {
        if (target != Owner.PetOwner?.Creature || Owner.IsDead || !props.IsPoweredAttack())
            return target;
        return Owner;
    }

    // 原版对所有生物都这样判断，不只针对持有者。
    public override bool ShouldAllowHitting(Creature creature) => creature.IsAlive;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) => creature != Owner;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}
