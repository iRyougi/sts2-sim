using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Regent starting relic. Grants 3 Stars whenever its owner enters a combat room.</summary>
public sealed class DivineRight : RelicModel
{
    private const decimal StarsGranted = 3m;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            await PlayerCmd.GainStars(StarsGranted, Owner);
        }
    }
}
