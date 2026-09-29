using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Bulwark : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        2, 0, CardType.Skill, CardRarity.Uncommon, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 12m, 0, 0, 0,
        0m, 0m, 0m, 0m, 10m,
        0m, 3m, 0, 0,
        0m, 3m, false, null, null);
}
