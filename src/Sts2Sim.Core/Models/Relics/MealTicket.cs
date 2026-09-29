using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MealTicket : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override Task AfterRoomEntered(AbstractRoom room) =>
        room is MerchantRoom && Owner.Creature.IsAlive
            ? CreatureCmd.Heal(Owner.Creature, 15m)
            : Task.CompletedTask;
}
