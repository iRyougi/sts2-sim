using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Silent starter Strike. The real source contains only numerical/static metadata expressible by
/// <see cref="GeneratedCardModel"/>: 6 damage, upgraded by 3, with the Strike tag.
/// </summary>
public sealed class StrikeSilent : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        6m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        3m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override IReadOnlyCollection<CardTag> CanonicalTags => new[] { CardTag.Strike };
}
