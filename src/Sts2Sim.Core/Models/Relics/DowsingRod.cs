using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DowsingRod : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        var card = (Dowsing)ModelDb.Card<Dowsing>().MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(card);
    }
}
