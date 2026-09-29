using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class SevenStars : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        2, 7, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies,
        false, false, false, Array.Empty<CardKeyword>(),
        7m, 7, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, true, null, null);
}
