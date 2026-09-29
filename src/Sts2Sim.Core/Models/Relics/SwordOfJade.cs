using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SwordOfJade : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom || Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            combatState,
            Owner.Creature,
            3m,
            Owner.Creature,
            cardSource: null);
    }
}
