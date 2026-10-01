using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

/// <summary>Native Tezcatara's Ember: a permanent free, Eternal card with +3 powered attack damage.</summary>
public sealed class TezcatarasEmber : EnchantmentModel
{
    public override void OnAttached(CardModel card)
    {
        card.ReduceEnergyCostFromEnchantment(card.LocalEnergyCost);
        card.AddKeywordInternal(CardKeyword.Eternal);
    }

    public override decimal EnchantDamageAdditive(decimal amount, ValueProp props) =>
        props.IsPoweredAttack() ? 3m : 0m;
}
