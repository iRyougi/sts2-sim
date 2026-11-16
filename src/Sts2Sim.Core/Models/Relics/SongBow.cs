using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Fool 起始遗物 SongBow（原版 <c>MegaCrit.Sts2.Core.Models.Relics.SongBow</c>）。</summary>
public sealed class SongBow : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom && Owner.Creature.CombatState is { } combatState)
        {
            await PowerCmd.Apply<AimPower>(combatState, Owner.Creature, 6m, Owner.Creature, null);
        }
    }
}
