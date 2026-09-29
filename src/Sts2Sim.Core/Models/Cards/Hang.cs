using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Hang</c>：造成 10（升级 13）伤害，然后把目标的 <see cref="HangPower"/> 翻倍（至少施加 2，
/// 总层数封顶 999999999）。</summary>
public sealed class Hang : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private decimal Damage => IsUpgraded ? 13m : 10m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        int powerAmount = cardPlay.Target.GetPower<HangPower>()?.Amount ?? 0;
        int amount = Math.Max(2, powerAmount);
        if (powerAmount + amount > 999999999)
            amount = Math.Max(0, 999999999 - powerAmount);
        await PowerCmd.Apply<HangPower>(CombatState!, cardPlay.Target, amount, Owner.Creature, this);
    }
}
