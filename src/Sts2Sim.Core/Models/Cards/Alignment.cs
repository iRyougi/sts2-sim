using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Alignment : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 2, CardType.Skill, CardRarity.Uncommon, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 2, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null,
        UpgradeGainEnergy: 1);
}
