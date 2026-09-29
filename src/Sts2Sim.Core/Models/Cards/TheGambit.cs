using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class TheGambit : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Rare, TargetType.Self,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 50m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 25m, 0, 0,
        0m, 0m, false, null, null);
}
