using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class RoyallyApproved : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) => card.Type is CardType.Attack or CardType.Skill && base.CanEnchant(card);

    public override void OnAttached(CardModel card)
    {
        card.AddKeywordInternal(CardKeyword.Innate);
        card.AddKeywordInternal(CardKeyword.Retain);
    }
}
