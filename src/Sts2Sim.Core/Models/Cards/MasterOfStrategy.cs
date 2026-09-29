using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class MasterOfStrategy : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Rare, TargetType.Self,
        true, false, false, new[] { CardKeyword.Exhaust },
        0m, 1, 0m, 3, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 1, 0,
        0m, 0m, false, null, null);
}
