namespace Sts2Sim.Core.Reporting;

public sealed record CombatLogRef(
    string CombatId,
    int Floor,
    string CombatLogFile);
