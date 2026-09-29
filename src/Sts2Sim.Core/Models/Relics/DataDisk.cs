using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DataDisk : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom combatRoom) return;
        Flash();
        await PowerCmd.Apply<FocusPower>(combatRoom.Engine.State, Owner.Creature,
            1m, Owner.Creature, null);
    }
}
