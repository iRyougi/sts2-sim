using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;
namespace Sts2Sim.Core.Models.Relics;

public sealed class NutritiousSoup : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterObtained()
    {
        foreach (CardModel card in Owner.Deck.Cards.ToArray())
        {
            if (card.Rarity == CardRarity.Basic &&
                card.Tags.Contains(CardTag.Strike) &&
                ((TezcatarasEmber)ModelDb.Get(typeof(TezcatarasEmber))).CanEnchant(card))
            {
                await CardCmd.Enchant<TezcatarasEmber>(card, 1m);
            }
        }
    }
}
