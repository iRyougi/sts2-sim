using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Volley : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy,
        true, true, false, Array.Empty<CardKeyword>(),
        10m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        4m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
