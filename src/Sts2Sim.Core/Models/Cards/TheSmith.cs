using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class TheSmith : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 4, CardType.Skill, CardRarity.Rare, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 30m,
        0m, 0m, 0, 0,
        0m, 10m, false, null, null);
}
