using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class InsatiableDominion : DominionModel
{
    private const int MiasmaAmount = 1;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom { CombatState: { } combatState })
        {
            foreach (Player player in combatState.Players)
            {
                await PowerCmd.Apply<InsatiableDominionPower>(
                    combatState, player.Creature, MiasmaAmount, applier: null, cardSource: null);
            }
        }
    }
}
