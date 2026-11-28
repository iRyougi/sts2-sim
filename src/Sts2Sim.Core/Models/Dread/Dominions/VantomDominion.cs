using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class VantomDominion : DominionModel
{
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        if (room is CombatRoom { CombatState: { } combatState } combatRoom)
        {
            int amount = combatRoom.RoomType == RoomType.Elite ? 2 : 1;
            foreach (Creature enemy in combatState.Enemies.ToArray())
            {
                await PowerCmd.Apply<SlipperyPower>(
                    combatState, enemy, amount, applier: null, cardSource: null);
            }
        }
    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        await base.AfterCreatureAddedToCombat(creature);
        if (creature.Side != CombatSide.Player)
        {
            await PowerCmd.Apply<SlipperyPower>(
                creature.CombatState!, creature, 1m, applier: null, cardSource: null);
        }
    }
}
