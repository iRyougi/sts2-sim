using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>征服者(-力量类，SovereignBlade对其伤害翻倍)。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.ConquerorPower</c>），
/// 自身回合结束时递减1层（原版 PowerCmd.Decrement）。</summary>
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
            // 原版 PowerCmd.Decrement：直接减一层，不看 SkipNextDurationTick
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        }
    }
}
