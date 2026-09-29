using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>TimesUp</c>：2 费，造成等于目标 Doom 层数的伤害（CalculationBase 0 + 1×Doom）；升级获得保留。</summary>
public sealed class TimesUp : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 2;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    // 原版 Thrash 读 CalculatedDamage.Calculate(null)，无目标时乘数为 0。
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CalculatedDamage(null);
        return true;
    }

    private decimal CalculatedDamage(Creature? target)
    {
        decimal doom = CombatState is not { } combatState || !combatState.IsLiveCombat()
            ? 0m
            : target?.GetPower<DoomPower>()?.Amount ?? 0;
        return 0m + 1m * doom;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(CalculatedDamage(cardPlay.Target)).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
