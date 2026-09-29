using Sts2Sim.Core.Map;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Rl;

/// <summary>Accumulates and decomposes progress, survival, and terminal rewards.</summary>
public sealed class RewardTracker
{
    public const double FloorShapingWeight = 0.01;
    public const double CombatWinBonus = 0.0;
    public const double TerminalWinBonus = 1.0;
    public const double TerminalLossPenalty = 0.0;

    private readonly RewardWeights _weights;
    private readonly List<(MapPoint Point, RoomType RoomType)> _resolvedRooms = new();

    public RewardTracker(Action<Action<MapPoint, RoomType>> subscribe, RewardWeights? weights = null)
    {
        _weights = weights ?? new RewardWeights();
        subscribe(OnRoomResolved);
    }

    private void OnRoomResolved(MapPoint point, RoomType roomType) =>
        _resolvedRooms.Add((point, roomType));

    public double DrainStepReward(bool done, bool won) =>
        DrainStepRewardComponents(done, won).Total;

    public RewardComponents DrainStepRewardComponents(
        bool done,
        bool won,
        bool truncated = false,
        int actsCleared = 0,
        int finalHp = 0,
        int maxHp = 0)
    {
        double floor = _resolvedRooms.Count * _weights.FloorWeight;
        _resolvedRooms.Clear();

        double act = actsCleared * _weights.ActClearedBonus;
        if (!done)
        {
            return new RewardComponents(floor, 0d, 0d, 0d);
        }

        if (truncated)
        {
            return new RewardComponents(floor, act, 0d, 0d);
        }
        double hp = maxHp > 0
            ? Math.Clamp((double)finalHp / maxHp, 0d, 1d) * _weights.HpRetentionWeight
            : 0d;
        double terminal = won ? _weights.TerminalWinBonus : _weights.TerminalLossPenalty;
        return new RewardComponents(floor, act, hp, terminal);
    }
}
