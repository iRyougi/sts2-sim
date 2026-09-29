using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PreservedFog : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public async override Task AfterObtained()
    {
        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            Owner.RunState,
            Owner,
            Owner.Deck.Cards.Where(card => card.IsRemovable),
            minCount: 3,
            maxCount: 3,
            source: this);
        foreach (CardModel card in selected)
        {
            await CardPileCmd.RemoveFromDeck(Owner, card);
        }

        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Folly>()], Owner);
    }
}
