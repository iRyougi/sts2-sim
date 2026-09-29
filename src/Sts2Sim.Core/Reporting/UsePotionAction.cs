namespace Sts2Sim.Core.Reporting;

public sealed record UsePotionAction(
    string Potion,
    string? Target,
    IReadOnlyList<DamageDealtRecord> DamageDealt,
    IReadOnlyList<PowerApplicationRecord> PowersApplied) : ActionRecord
{
    public int HealingReceived { get; init; }

    public bool Consumed { get; init; }

    public IReadOnlyList<string> PotionsAdded { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PotionsRemoved { get; init; } = Array.Empty<string>();
}
