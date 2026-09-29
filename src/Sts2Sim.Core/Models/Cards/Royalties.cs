using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Royalties : GeneratedCardModel
{

    public override bool CanBeGeneratedInCombat => false;
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Power, CardRarity.Rare, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);
}
