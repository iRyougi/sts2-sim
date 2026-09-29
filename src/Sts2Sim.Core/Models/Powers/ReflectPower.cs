using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>反射，格挡了受powered攻击的伤害部分反弹给攻击者；自身回合开始时递减1层。
/// 逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.ReflectPower</c>）。</summary>
public sealed class ReflectPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.BlockedDamage > 0 && props.IsPoweredAttack() && dealer is not null)
        {
            await CreatureCmd.Damage(
                Owner.CombatState!, new[] { dealer }, result.BlockedDamage, ValueProp.Unpowered, Owner, null, null);
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.TickDownDuration(Owner.CombatState!, this);
        }
    }
}
