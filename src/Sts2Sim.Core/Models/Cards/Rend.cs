using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Rend : GeneratedCardModel
{
    // Native CalculatedDamage.Calculate(null) has no target powers to count.
    public override bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = IsUpgraded ? 12m : 10m;
        return true;
    }

    // v0.111.0 正式版：费用 1；伤害 = 基础 10（升级 +2）+ 每个非临时减益 5（升级 +3）。
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy,
        true, false, false, Array.Empty<CardKeyword>(),
        10m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        2m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        // 原版按 TypeForCurrentAmount（= GetTypeForAmount(Amount)）判断减益：例如负力量算减益。
        int debuffCount = cardPlay.Target.Powers.Count(power =>
            power.GetTypeForAmount(power.Amount) == PowerType.Debuff &&
            power is not ITemporaryPower);
        decimal baseDamage = IsUpgraded ? 12m : 10m;
        decimal damagePerDebuff = IsUpgraded ? 8m : 5m;
        await DamageCmd.Attack(baseDamage + damagePerDebuff * debuffCount)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
    }
}
