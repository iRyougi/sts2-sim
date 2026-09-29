using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Lift : GeneratedCardModel
{
    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 11m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 5m, 0, 0,
        0m, 0m, false, null, null);
}
