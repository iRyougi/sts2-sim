using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Grants 1 Strength whenever its owner enters a combat room.</summary>
public sealed class Vajra : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom combatRoom)
        {
            await PowerCmd.Apply<StrengthPower>(combatRoom.Engine.State, Owner.Creature, 1m, applier: null, cardSource: null);
        }
    }
}
