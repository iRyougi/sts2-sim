namespace Sts2Sim.Core.Reporting;

public sealed record CombatEnemyInitialSnapshot(
    string Slot,
    string Id,
    int Hp,
    int MaxHp,
    IReadOnlyList<PowerSnapshot> Powers);
