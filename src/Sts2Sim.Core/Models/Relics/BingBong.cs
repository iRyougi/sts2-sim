using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BingBong : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        if (card.Pile?.Type != PileType.Deck ||
            !ReferenceEquals(card.Owner, Owner) ||
            clonedBy is not null)
        {
            return;
        }

        var copy = (CardModel)card.MutableClone();
        copy.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(copy, this);
    }
}
