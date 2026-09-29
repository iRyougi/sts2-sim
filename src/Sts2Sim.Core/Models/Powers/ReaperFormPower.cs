using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>ReaperFormPower</c>：持有者或其宠物（Osty）的有力攻击造成伤害（含被格挡部分）后，
/// 给受击者施加 伤害总量×层数 的 Doom。原版 AfterApplied/AfterRemoved 只创建/关闭特效，这里省略。</summary>
public sealed class ReaperFormPower : PowerModel
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
            (dealer == Owner || dealer.PetOwner?.Creature == Owner) &&
            props.IsPoweredAttack() &&
            result.TotalDamage > 0)
        {
            await PowerCmd.Apply<DoomPower>(Owner.CombatState!, target, result.TotalDamage * Amount, Owner, null);
        }
    }
}
