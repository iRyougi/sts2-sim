using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Mayhem : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        2, 0, CardType.Power, CardRarity.Rare, TargetType.Self,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, true, null, null);
}
