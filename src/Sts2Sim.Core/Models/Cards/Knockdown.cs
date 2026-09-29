using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Knockdown : GeneratedCardModel
{
    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        3, 0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy,
        true, false, false, Array.Empty<CardKeyword>(),
        10m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        4m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
