using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BronzeScales : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom combatRoom)
        {
            await PowerCmd.Apply<ThornsPower>(
                combatRoom.Engine.State,
                Owner.Creature,
                3m,
                Owner.Creature,
                null);
        }
    }
}
