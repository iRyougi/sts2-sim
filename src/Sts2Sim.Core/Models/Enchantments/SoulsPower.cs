using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

/// <summary>Grave enchantment that consumes a card's local Exhaust keyword.</summary>
public sealed class SoulsPower : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card) && card.HasKeyword(CardKeyword.Exhaust);

    public override void OnAttached(CardModel card) =>
        card.RemoveKeywordInternal(CardKeyword.Exhaust);
}
