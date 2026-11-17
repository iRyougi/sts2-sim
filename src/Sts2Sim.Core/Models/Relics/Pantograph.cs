using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Pantograph : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (Owner.Creature.IsDead)
        {
            return Task.CompletedTask;
        }

        // Source: Active while the current map point is a parent of the Boss map point, otherwise Normal.
        // Owner.RunState that is not a concrete RunState (test facades) has no map and is left untouched.
        if (Owner.RunState is RunState runState)
        {
            bool flag = runState.CurrentMapPoint is { } current && runState.Map.BossMapPoint.parents.Contains(current);
            Status = flag ? RelicStatus.Active : RelicStatus.Normal;
        }

        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        if (!Owner.Creature.IsAlive || Owner.RunState.CurrentRoom?.RoomType != RoomType.Boss)
            return Task.CompletedTask;

        Flash();
        return CreatureCmd.Heal(Owner.Creature, 25m);
    }
}
