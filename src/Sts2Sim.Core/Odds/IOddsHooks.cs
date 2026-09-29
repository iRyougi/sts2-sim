using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Odds;

/// <summary>
/// Odds 系统对游戏全局 Hook 系统的依赖切面。Plan 2 建好 Hook 调度器后由其实现；
/// 在那之前用 NullOddsHooks（等价于"没有任何遗物/事件修改概率"）。
/// 对应游戏 Hook：ShouldForcePotionReward / ModifyUnknownMapPointRoomTypes /
/// ModifyOddsIncreaseForUnrolledRoomType。
/// </summary>
public interface IOddsHooks
{
    bool ShouldForcePotionReward(RoomType roomType);

    IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes);

    float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float increase);
}

public sealed class NullOddsHooks : IOddsHooks
{
    public static readonly NullOddsHooks Instance = new();

    private NullOddsHooks()
    {
    }

    public bool ShouldForcePotionReward(RoomType roomType) => false;

    public IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes) => roomTypes;

    public float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float increase) => increase;
}
