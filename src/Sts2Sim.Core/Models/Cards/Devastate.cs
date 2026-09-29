using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Devastate : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 4, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        35m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        10m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
