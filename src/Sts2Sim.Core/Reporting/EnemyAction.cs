namespace Sts2Sim.Core.Reporting;

public sealed record EnemyAction(
    string Source,
    string MoveId,
    IReadOnlyList<DamageTakenRecord> DamageToTargets,
    IReadOnlyList<PowerApplicationRecord> PowersApplied) : ActionRecord;
