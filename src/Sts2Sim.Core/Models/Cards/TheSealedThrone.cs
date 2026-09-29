using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class TheSealedThrone : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 3, CardType.Power, CardRarity.Ancient, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, true, null, null);
}
