using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsHorn : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        for (int i = 0; i < 2; i++)
        {
            var card = (Relax)ModelDb.Card<Relax>().MutableClone();
            card.AssignOwner(Owner);
            await CardPileCmd.AddToDeck(card);
        }
    }
}
