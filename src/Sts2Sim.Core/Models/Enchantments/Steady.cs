using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Steady : EnchantmentModel
{
    public override void OnAttached(CardModel card) => card.AddKeywordInternal(CardKeyword.Retain);
}
