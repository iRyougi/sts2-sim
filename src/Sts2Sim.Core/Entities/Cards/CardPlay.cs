using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Entities.Cards;

/// <summary>代表一次具体的出牌。偏离 #105（部分解决 #32）：`PlayIndex`/`PlayCount` 现由
/// <see cref="CardModel.PlayAsync"/> 依据 <see cref="CardModel.BaseReplayCount"/> 循环生成；
/// 无 Replay 的卡这两个值仍恒为 0/1。</summary>
public sealed class CardPlay
{
    public required CardModel Card { get; init; }

    public required Player Player { get; init; }

    public required Creature? Target { get; init; }

    public required PileType ResultPile { get; init; }

    public required ResourceInfo Resources { get; init; }

    public required bool IsAutoPlay { get; init; }

    public required int PlayIndex { get; init; }

    public required int PlayCount { get; init; }

    /// <summary>Stable ordinal of this CardPlay among the player's starts in this turn.</summary>
    public int PlayOrdinal { get; init; }

    public bool IsFirstInSeries => PlayIndex == 0;

    public bool IsLastInSeries => PlayIndex == PlayCount - 1;
}
