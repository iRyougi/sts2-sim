using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class KnowThyPlace : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy,
        false, false, false, new[] { CardKeyword.Exhaust },
        0m, 1, 0m, 0, 0, 0,
        1m, 1m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, CardKeyword.Exhaust);
}
