using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Pantograph : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        // Native updates only the relic's presentation status here.
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        if (!Owner.Creature.IsAlive || Owner.RunState.CurrentRoom?.RoomType != RoomType.Boss)
            return Task.CompletedTask;

        Flash();
        return CreatureCmd.Heal(Owner.Creature, 25m);
    }
}
