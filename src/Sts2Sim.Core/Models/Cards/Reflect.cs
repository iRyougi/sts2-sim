using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Reflect : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 3, CardType.Skill, CardRarity.Uncommon, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 15m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 5m, 0, 0,
        0m, 0m, false, null, null);
}
