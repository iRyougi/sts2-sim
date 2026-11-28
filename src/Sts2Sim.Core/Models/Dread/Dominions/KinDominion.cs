using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class KinDominion : DominionModel
{
    private const int DebuffAmount = 2;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom { CombatState: { } combatState })
        {
            foreach (Player player in combatState.Players)
            {
                await PowerCmd.Apply<WeakPower>(
                    combatState, player.Creature, DebuffAmount, applier: null, cardSource: null);
            }

            foreach (Player player in combatState.Players)
            {
                await PowerCmd.Apply<FrailPower>(
                    combatState, player.Creature, DebuffAmount, applier: null, cardSource: null);
            }
        }
    }
}
