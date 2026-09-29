using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Instinct : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Attack;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        ReferenceEquals(cardSource, Owner) && props.IsPoweredAttack() ? 2m : 1m;
}
