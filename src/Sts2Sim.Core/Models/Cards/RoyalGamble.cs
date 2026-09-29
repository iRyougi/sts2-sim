using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class RoyalGamble : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 5, CardType.Skill, CardRarity.Uncommon, TargetType.Self,
        false, false, false, new[] { CardKeyword.Exhaust },
        0m, 1, 0m, 0, 0, 9,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, CardKeyword.Retain, null);
}
