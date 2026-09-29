using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Spiral : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card) && card.Rarity == CardRarity.Basic &&
        (card.Tags.Contains(CardTag.Strike) || card.Tags.Contains(CardTag.Defend));

    public override int EnchantPlayCount(int originalPlayCount) => originalPlayCount + 1;
}
