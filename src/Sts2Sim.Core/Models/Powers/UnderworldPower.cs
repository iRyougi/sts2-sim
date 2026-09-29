using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>UnderworldPower</c>（仅限多人的 Underworld 施加）：同阵营中除持有者本人及其宠物外的
/// 任意生物以有力攻击造成伤害（含被格挡部分）后，给受击者施加 伤害总量×层数 的 Doom；
/// 敌方回合结束时移除。</summary>
public sealed class UnderworldPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageGiven(
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer is not null &&
            dealer.Side == Owner.Side &&
            dealer != Owner &&
            dealer.PetOwner != Owner.Player &&
            props.IsPoweredAttack() &&
            result.TotalDamage > 0)
        {
            await PowerCmd.Apply<DoomPower>(Owner.CombatState!, target, result.TotalDamage * Amount, Owner, null);
        }
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
            await PowerCmd.Remove(this);
    }
}
