using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class SolarStrike : GeneratedCardModel
{

    protected override IReadOnlyCollection<CardTag> CanonicalTags => new[] { CardTag.Strike };
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        9m, 1, 0m, 0, 0, 1,
        0m, 0m, 0m, 0m, 0m,
        1m, 0m, 0, 1,
        0m, 0m, false, null, null);
}
