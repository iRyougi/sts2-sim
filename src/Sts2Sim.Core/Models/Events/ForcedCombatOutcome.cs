namespace Sts2Sim.Core.Models.Events;

/// <summary>一场事件内强制战斗的结果，回传给挂起中的事件用于派奖判定。</summary>
/// <param name="Victory">敌方是否被全部击败。</param>
/// <param name="TimedOut">是否有任一强制战斗生物逃跑。</param>
public readonly record struct ForcedCombatOutcome(bool Victory, bool TimedOut);
