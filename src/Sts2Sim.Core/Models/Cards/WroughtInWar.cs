using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class WroughtInWar : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        7m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 7m,
        2m, 0m, 0, 0,
        0m, 2m, false, null, null);
}
