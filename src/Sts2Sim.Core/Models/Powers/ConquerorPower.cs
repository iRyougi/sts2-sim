using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>征服者(-力量类，SovereignBlade对其伤害翻倍)。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.ConquerorPower</c>），
/// 每回合自身回合开始时递减1层（原版 PowerCmd.Decrement）。</summary>
public sealed class ConquerorPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (cardSource is not SovereignBlade || !props.IsPoweredAttack() || target != Owner)
        {
            return 1m;
        }

        return 2m;
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            // 原版是 PowerCmd.Decrement（直接减一层），不看 SkipNextDurationTick：敌人回合里刚挂上的减益当回合结束就减
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        }
    }
}
