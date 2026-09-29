namespace Sts2Sim.Core.Reporting;

public sealed record EnemySnapshot(
    string Slot,
    string Id,
    int Hp,
    int MaxHp,
    int Block,
    IReadOnlyList<PowerSnapshot> Powers,
    string Intent);
