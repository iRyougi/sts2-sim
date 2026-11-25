namespace Sts2Sim.Core.Entities.Creatures;

/// <summary>一次扣血（或空跑扣血）的结果。逐字移植 v0.114.0 <c>MegaCrit.Sts2.Core.Entities.Creatures.HpLossResult</c>。</summary>
public readonly record struct HpLossResult(int HpLost, bool Killed, int OverkillAmount);
