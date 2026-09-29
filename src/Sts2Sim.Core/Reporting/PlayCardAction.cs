namespace Sts2Sim.Core.Reporting;

public sealed record PlayCardAction(
    string Card,
    string? Target,
    int EnergyCost,
    IReadOnlyList<DamageDealtRecord> DamageDealt,
    IReadOnlyList<PowerApplicationRecord> PowersApplied,
    int BlockGained,
    int PlayCount = 1) : ActionRecord;
