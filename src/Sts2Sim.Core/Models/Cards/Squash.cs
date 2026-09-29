using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Squash : GeneratedCardModel
{
    private static readonly GeneratedCardSpec CardSpec = new(
        1, 0, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        10m, 1, 0m, 0, 0, 0, 0m, 2m, 0m, 0m, 0m,
        2m, 0m, 0, 0, 0m, 0m, false, null, null, UpgradeVulnerable: 1m);

    protected override GeneratedCardSpec Spec => CardSpec;
}
