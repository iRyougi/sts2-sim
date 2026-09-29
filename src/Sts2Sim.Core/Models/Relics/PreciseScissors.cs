using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PreciseScissors : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        foreach (CardModel selected in await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, Owner.Deck.Cards.Where(card => card.IsRemovable), 1, 1, this))
        {
            await CardPileCmd.RemoveFromDeck(Owner, selected);
        }
    }
}
