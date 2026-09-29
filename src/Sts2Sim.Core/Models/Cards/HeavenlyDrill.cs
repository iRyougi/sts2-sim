using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

public sealed class HeavenlyDrill : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy,
        false, true, false, Array.Empty<CardKeyword>(),
        8m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        2m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override int CanonicalStarCost => -1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int x = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyXValue);
        int hitCount = x >= 4 ? x * 2 : x;
        decimal damage = IsUpgraded ? 10m : 8m;
        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
    }
}
