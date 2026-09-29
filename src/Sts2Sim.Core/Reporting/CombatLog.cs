namespace Sts2Sim.Core.Reporting;

public sealed record CombatLog(
    string SchemaVersion,
    string RunId,
    string CombatId,
    int Floor,
    string EncounterType,
    string EncounterName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    string Result,
    CombatPlayerInitialSnapshot PlayerInitial,
    IReadOnlyList<CombatEnemyInitialSnapshot> EnemiesInitial,
    IReadOnlyList<TurnRecord> Turns,
    CombatRewards Rewards);
