using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Stardust : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy,
        false, false, true, Array.Empty<CardKeyword>(),
        5m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        2m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
