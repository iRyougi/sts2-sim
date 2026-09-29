using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Finesse : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Uncommon, TargetType.Self,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 4m, 1, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 3m, 0, 0,
        0m, 0m, false, null, null);
}
