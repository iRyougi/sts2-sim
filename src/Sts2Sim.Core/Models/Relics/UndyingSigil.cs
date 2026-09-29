using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>当前生命不超过自身 Doom 层数的攻击者，对自己造成的有威力攻击伤害减半。</summary>
public sealed class UndyingSigil : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer is null || !props.IsPoweredAttack() || target != Owner.Creature || dealer == Owner.Creature)
            return 1m;
        if (dealer.CurrentHp > (dealer.GetPower<DoomPower>()?.Amount ?? 0))
            return 1m;
        return 0.5m;
    }
}
