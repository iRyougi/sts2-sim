using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Sharp : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) => card.Type == CardType.Attack && base.CanEnchant(card);
    public override decimal EnchantDamageAdditive(decimal amount, ValueProp props) =>
        props.IsPoweredAttack() ? Magnitude : 0m;
}
