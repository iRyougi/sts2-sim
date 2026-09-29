using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class EternalFeather : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not RestSiteRoom || !Owner.Creature.IsAlive)
        {
            return Task.CompletedTask;
        }

        int healAmount = 3 * (Owner.Deck.Cards.Count / 5);
        return CreatureCmd.Heal(Owner.Creature, healAmount);
    }
}
