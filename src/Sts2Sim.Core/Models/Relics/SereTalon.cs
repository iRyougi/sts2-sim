using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SereTalon : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;
    public override async Task AfterObtained()
    {
        await CreatureCmd.LoseMaxHp(Owner.RunState, Owner.Creature, 9m, isFromCard: false);
        for (int index = 0; index < 3; index++)
        {
            var wish = (Wish)ModelDb.Card<Wish>().MutableClone();
            wish.AssignOwner(Owner);
            await CardPileCmd.AddToDeck(wish);
        }
    }
}
