namespace Sts2Sim.Core.Reporting;

public sealed record ActionSnapshotSegment(
    PlayerSnapshot PlayerBefore,
    PlayerSnapshot PlayerAfter,
    IReadOnlyList<EnemySnapshot> EnemiesBefore,
    IReadOnlyList<EnemySnapshot> EnemiesAfter);
