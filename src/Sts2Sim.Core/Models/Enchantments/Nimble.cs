namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Nimble : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.GainsBlock;
    public override decimal EnchantBlockAdditive(decimal amount) => Magnitude;
}
