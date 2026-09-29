using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class MonarchsGaze : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        2, 0, CardType.Power, CardRarity.Rare, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, true, null, null);
}
