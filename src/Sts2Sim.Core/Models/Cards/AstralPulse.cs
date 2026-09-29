using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class AstralPulse : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 3, CardType.Attack, CardRarity.Common, TargetType.AllEnemies,
        false, false, false, Array.Empty<CardKeyword>(),
        6m, 2, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        2m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
