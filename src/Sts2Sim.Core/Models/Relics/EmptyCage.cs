using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class EmptyCage : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        foreach (CardModel card in await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, Owner.Deck.Cards.Where(card => card.IsRemovable), 2, 2, this))
            await CardPileCmd.RemoveFromDeck(Owner, card);
    }
}
