using Sts2Sim.Core.Map;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Rl;

public class RewardTrackerTests
{
    private sealed class FakeRunDriverEvents
    {
        public event Action<MapPoint, RoomType>? OnRoomResolved;

        public void Raise(MapPoint point, RoomType roomType) => OnRoomResolved?.Invoke(point, roomType);
    }

    [Fact]
    public void DrainStepReward_NoRoomsResolved_ReturnsZero()
    {
        var events = new FakeRunDriverEvents();
        var tracker = new RewardTracker(handler => events.OnRoomResolved += handler);

        double reward = tracker.DrainStepReward(done: false, won: false);

        Assert.Equal(0d, reward);
    }

    [Fact]
    public void DrainStepReward_NonCombatRoomResolved_OnlyAddsFloorShaping()
    {
        var events = new FakeRunDriverEvents();
        var tracker = new RewardTracker(handler => events.OnRoomResolved += handler);
        events.Raise(new MapPoint(0, 1), RoomType.RestSite);

        double reward = tracker.DrainStepReward(done: false, won: false);

        Assert.Equal(RewardTracker.FloorShapingWeight, reward, precision: 6);
    }

    [Fact]
    public void DrainStepReward_CombatRoomWon_AddsFloorShapingAndCombatBonus()
    {
        var events = new FakeRunDriverEvents();
        var tracker = new RewardTracker(handler => events.OnRoomResolved += handler);
        events.Raise(new MapPoint(0, 1), RoomType.Monster);

        double reward = tracker.DrainStepReward(done: false, won: false);

        Assert.Equal(RewardTracker.FloorShapingWeight + RewardTracker.CombatWinBonus, reward, precision: 6);
    }

    [Fact]
    public void DrainStepReward_DoneAndWon_AddsTerminalWinBonus()
    {
        var events = new FakeRunDriverEvents();
        var tracker = new RewardTracker(handler => events.OnRoomResolved += handler);
        events.Raise(new MapPoint(0, 1), RoomType.Boss);

        double reward = tracker.DrainStepReward(done: true, won: true);

        Assert.Equal(
            RewardTracker.FloorShapingWeight + RewardTracker.CombatWinBonus + RewardTracker.TerminalWinBonus,
            reward,
            precision: 6);
    }

    [Fact]
    public void DrainStepReward_DoneAndLost_LastCombatDoesNotGetWinBonus()
    {
        var events = new FakeRunDriverEvents();
        var tracker = new RewardTracker(handler => events.OnRoomResolved += handler);
        events.Raise(new MapPoint(0, 1), RoomType.Monster);

        double reward = tracker.DrainStepReward(done: true, won: false);

        Assert.Equal(
            RewardTracker.FloorShapingWeight + RewardTracker.TerminalLossPenalty,
            reward,
            precision: 6);
    }

    [Fact]
    public void DrainStepReward_ClearsBufferBetweenCalls()
    {
        var events = new FakeRunDriverEvents();
        var tracker = new RewardTracker(handler => events.OnRoomResolved += handler);
        events.Raise(new MapPoint(0, 1), RoomType.RestSite);
        tracker.DrainStepReward(done: false, won: false);

        double secondReward = tracker.DrainStepReward(done: false, won: false);

        Assert.Equal(0d, secondReward);
    }
}
