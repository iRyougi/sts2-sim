using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Shockwave : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        2, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies,
        true, false, false, new[] { CardKeyword.Exhaust },
        0m, 1, 0m, 0, 0, 0,
        3m, 3m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null, UpgradeWeak: 2m, UpgradeVulnerable: 2m);
}
