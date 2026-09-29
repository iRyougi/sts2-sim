using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class UltimateDefend : GeneratedCardModel
{

    protected override IReadOnlyCollection<CardTag> CanonicalTags => new[] { CardTag.Defend };
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Uncommon, TargetType.Self,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 11m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 4m, 0, 0,
        0m, 0m, false, null, null);
}
