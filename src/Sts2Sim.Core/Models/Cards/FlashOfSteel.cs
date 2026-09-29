using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class FlashOfSteel : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        true, false, false, Array.Empty<CardKeyword>(),
        5m, 1, 0m, 1, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        3m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
