using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.Relics;

public sealed class NeowsSacrifice : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        var ambergris = (Ambergris)ModelDb.Potion<Ambergris>().MutableClone();
        await PotionCmd.TryToProcure(ambergris, Owner);

        var guilty = (Guilty)ModelDb.Card<Guilty>().MutableClone();
        guilty.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(guilty);
    }
}
