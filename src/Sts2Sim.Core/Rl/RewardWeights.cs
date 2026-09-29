namespace Sts2Sim.Core.Rl;

/// <summary>Injectable weights for the run-level progress and survival reward.</summary>
public sealed record RewardWeights(
    double FloorWeight = 0.01,
    double ActClearedBonus = 0.20,
    double HpRetentionWeight = 0.30,
    double TerminalWinBonus = 1.0,
    double TerminalLossPenalty = 0.0);
