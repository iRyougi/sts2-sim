using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class LagavulinMatriarchDominion : DominionModel
{
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom { CombatState: { } combatState })
        {
            foreach (Player player in combatState.Players)
            {
                await PowerCmd.Apply<LagavulinMatriarchDominionPower>(
                    combatState, player.Creature, 1m, applier: null, cardSource: null);
            }
        }
    }
}
