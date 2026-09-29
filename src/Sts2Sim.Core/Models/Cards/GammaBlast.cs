using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class GammaBlast : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        13m, 1, 0m, 0, 0, 0,
        2m, 2m, 0m, 0m, 0m,
        5m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
