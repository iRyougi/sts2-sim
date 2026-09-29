namespace Sts2Sim.Core.Reporting;

public sealed record CombatPlayerInitialSnapshot(
    int Hp,
    int MaxHp,
    int Block,
    int Energy,
    int DeckSize,
    IReadOnlyList<string?> RelicIds,
    IReadOnlyList<string?> PotionIds);
