namespace Sts2Sim.Core.Reporting;

public sealed record RunManifest(
    string SchemaVersion,
    string RunId,
    string Seed,
    string Character,
    int Ascension,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    string Result,
    int FinalHp,
    int MaxHp,
    int FloorsVisited,
    IReadOnlyList<FloorEntry> Floors)
{
    public IReadOnlyList<CombatLogRef> CombatLogs { get; init; } = Array.Empty<CombatLogRef>();

    /// <summary>本局是否打到并结算过任意一幕的 Boss。<see cref="Result"/> 只表达"是否通关"，
    /// 打通前几幕但倒在后续幕的局，其 <see cref="Result"/> 仍是 defeat。</summary>
    public bool ReachedBoss { get; init; }

    /// <summary>结束时玩家是否仍然存活。用于把"被 <c>maxFloors</c> 截断"与"死亡"区分开。</summary>
    public bool Survived { get; init; } = true;

    /// <summary>本局是否因 <c>maxFloors</c> 上限提前收束，而非死亡或通关。</summary>
    public bool Truncated { get; init; }

    /// <summary>已打通的幕数（打通该幕 Boss 即计一幕）。</summary>
    public int ActsCleared { get; init; }
}
