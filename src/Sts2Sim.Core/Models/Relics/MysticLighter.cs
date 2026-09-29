using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MysticLighter : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        // 逐条对照上游 MysticLighter.ModifyDamageAdditive：判据是 cardSource?.Enchantment == null
        // 即不加伤——触发条件是**卡牌带附魔**，不是已升级。原偏离 #136 以升级状态替代是因为当时
        // 没有附魔系统；附魔系统已存在（CardModel.AttachEnchantment，单卡至多一个），故销案。
        return props.IsPoweredAttack() &&
            dealer == Owner.Creature &&
            cardSource?.Owner == Owner &&
            cardSource.Enchantments.Count > 0
                ? 9m
                : 0m;
    }
}
