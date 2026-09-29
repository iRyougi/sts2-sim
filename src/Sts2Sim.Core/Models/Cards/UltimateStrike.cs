using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class UltimateStrike : GeneratedCardModel
{

    protected override IReadOnlyCollection<CardTag> CanonicalTags => new[] { CardTag.Strike };
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        true, false, false, Array.Empty<CardKeyword>(),
        14m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        6m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
