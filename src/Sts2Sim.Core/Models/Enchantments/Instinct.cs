using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Instinct : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Attack;

    // sls2：原版 Instinct.EnchantDamageMultiplicative（由 Hook.ModifyDamage 在全部监听者之前作用于卡面伤害）。
    // 原来写成监听者的 ModifyDamageMultiplicative，排在力量之后才乘：打击 6、力量 4 算成 (6 + 4) × 2 = 20，原版 6 × 2 + 4 = 16
    public override decimal EnchantDamageMultiplicative(decimal amount, ValueProp props) => props.IsPoweredAttack() ? 2m : 1m;
}
