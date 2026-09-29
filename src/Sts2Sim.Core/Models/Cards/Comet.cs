using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Comet : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 5, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        33m, 1, 0m, 0, 0, 0,
        3m, 3m, 0m, 0m, 0m,
        11m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
