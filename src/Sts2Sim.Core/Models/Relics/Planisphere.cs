using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Planisphere : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        // 原版：每进入一个未知地图点、且是该点房间栈的第一层（CurrentRoomCount <= 1）就回血。
        // 房间先 PushRoom 再 Enter，所以该点里嵌套进入的房间（事件里打起来的战斗等）深度 >= 2，不再回血。
        // room.Id 是整局递增的房间编号，不能拿来判断"第一个房间"。
        if (!Owner.Creature.IsAlive ||
            Owner.RunState is not RunState runState ||
            runState.CurrentMapPoint?.PointType != MapPointType.Unknown ||
            runState.CurrentRoomCount > 1)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.Heal(Owner.Creature, 5m);
    }
}
