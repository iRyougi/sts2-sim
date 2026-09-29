namespace Sts2Sim.Core.Reporting;

public sealed record PlayerSnapshot(
    int Hp,
    int MaxHp,
    int Block,
    int Energy,
    int Stars,
    IReadOnlyList<string> Hand,
    int DrawPileSize,
    int DiscardPileSize,
    int ExhaustPileSize,
    IReadOnlyList<PowerSnapshot> Powers);
