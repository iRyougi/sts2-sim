namespace Sts2Sim.Core.Reporting;

public sealed record TurnRecord(
    int TurnIndex,
    string Side,
    PlayerSnapshot PlayerPre,
    IReadOnlyList<EnemySnapshot> EnemiesPre,
    IReadOnlyList<DrawRecord> Draws,
    IReadOnlyList<ActionRecord> Actions,
    PlayerSnapshot PlayerPost,
    IReadOnlyList<EnemySnapshot> EnemiesPost);
