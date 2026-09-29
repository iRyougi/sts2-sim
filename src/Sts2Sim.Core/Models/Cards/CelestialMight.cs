using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CelestialMight : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        2, 0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        6m, 3, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(6m)
            .FromCard(this, cardPlay)
            .WithHitCount(IsUpgraded ? 4 : 3)
            .Targeting(cardPlay.Target)
            .Execute();
    }

}
