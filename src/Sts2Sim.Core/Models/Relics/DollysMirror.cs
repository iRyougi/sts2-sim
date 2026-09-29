using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DollysMirror : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(Owner.RunState, Owner,
            Owner.Deck.Cards.Where(card => card.Type != CardType.Quest),
            1, 1, this)).FirstOrDefault();
        if (selected is not null)
        {
            CardModel copy = (CardModel)selected.MutableClone();
            await CardPileCmd.AddToDeck(copy, selected);
        }
    }
}
