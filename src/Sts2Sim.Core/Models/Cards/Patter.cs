using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Patter : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Common, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 8m, 0, 0, 0,
        0m, 0m, 0m, 2m, 0m,
        0m, 2m, 0, 0,
        1m, 0m, false, null, null);
}
