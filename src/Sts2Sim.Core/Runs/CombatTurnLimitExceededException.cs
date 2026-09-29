using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Runs;

/// <summary>一场战斗超过了 <c>RunEngine</c> 的玩家回合上限。
///
/// 只有显式配置了上限才会抛（生产路径不配，行为不变）。用于把
/// "打不死的玩家陷入一场杀不掉怪的战斗"从挂起变成可观测的失败。</summary>
public sealed class CombatTurnLimitExceededException : InvalidOperationException
{
    public CombatTurnLimitExceededException(int limit, RoomType roomType)
        : base($"Combat in a {roomType} room exceeded the {limit}-player-turn limit without ending.")
    {
        Limit = limit;
        RoomType = roomType;
    }

    public int Limit { get; }

    public RoomType RoomType { get; }
}
