namespace Sts2Sim.Core.Reporting;

public sealed record CombatRewards(
    int Gold,
    IReadOnlyList<string> CardsOffered,
    string? CardTaken,
    string? RelicTaken,
    string? PotionTaken)
{
    public IReadOnlyList<string> CardsTaken { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RelicsTaken { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PotionsTaken { get; init; } = Array.Empty<string>();
}
