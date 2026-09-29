using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Exterminate : GeneratedCardModel
{
    private static readonly GeneratedCardSpec CardSpec = new(
        1, 0, CardType.Attack, CardRarity.Event, TargetType.AllEnemies,
        false, false, false, Array.Empty<CardKeyword>(),
        3m, 4, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
        1m, 0m, 0, 0, 0m, 0m, false, null, null);

    protected override GeneratedCardSpec Spec => CardSpec;
}
