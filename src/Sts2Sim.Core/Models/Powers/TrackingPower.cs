using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class TrackingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!props.IsPoweredAttack() || cardSource is null || target is null || !target.HasPower<WeakPower>()) return 1m;
        if (dealer != Owner) return 1m; // 偏离 #38：模拟器尚无宠物实体；玩家来源语义保持一致。
        return 1m + Amount / 100m;
    }
}
