using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Instinct : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Attack;

    // game Instinct.EnchantDamageMultiplicative (applied by Hook.ModifyDamage before all listeners).
    public override decimal EnchantDamageMultiplicative(decimal amount, ValueProp props) => props.IsPoweredAttack() ? 2m : 1m;
}
