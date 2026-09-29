using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Odds;

/// <summary>
/// IOddsHooks 的真实现:把 Plan 01 的 Odds seam 接到 Hook 静态调度器。
/// RunState(Plan 04)构造 PlayerOddsSet/RunOddsSet 时注入本适配器,替代 NullOddsHooks。
/// </summary>
public sealed class HookOddsAdapter(IRunState runState) : IOddsHooks
{
    public bool ShouldForcePotionReward(RoomType roomType)
    {
        return Hook.ShouldForcePotionReward(runState, roomType);
    }

    public IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes)
    {
        return Hook.ModifyUnknownMapPointRoomTypes(runState, roomTypes);
    }

    public float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float increase)
    {
        return Hook.ModifyOddsIncreaseForUnrolledRoomType(runState, roomType, increase);
    }
}
