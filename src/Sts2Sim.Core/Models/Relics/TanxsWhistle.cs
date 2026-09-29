using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TanxsWhistle : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public async override Task AfterObtained()
    {
        var card = (Whistle)ModelDb.Card<Whistle>().MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(card);
    }
}
