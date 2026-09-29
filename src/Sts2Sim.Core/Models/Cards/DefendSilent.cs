using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Silent starter Defend. The real source contains only numerical/static metadata expressible by
/// <see cref="GeneratedCardModel"/>: 5 block, upgraded by 3, with the Defend tag.
/// </summary>
public sealed class DefendSilent : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Basic, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 5m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 3m, 0, 0,
        0m, 0m, false, null, null);

    protected override IReadOnlyCollection<CardTag> CanonicalTags => new[] { CardTag.Defend };
}
