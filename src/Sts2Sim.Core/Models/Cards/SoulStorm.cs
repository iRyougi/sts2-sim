using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>SoulStorm</c>：1 费，造成 9 + 4（升级 6）×消耗堆中 <see cref="Soul"/> 张数 的伤害。</summary>
public sealed class SoulStorm : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const decimal CalculationBase = 9m;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private decimal ExtraDamage => IsUpgraded ? 6m : 4m;

    // CalculationBase/ExtraDamage/CalculatedDamage 都不是字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CalculatedDamage();
        return true;
    }

    // 原版 CalculatedVar.Calculate：战斗外乘数为 0。原版在 AttackCommand 执行每一击时才计算，
    // 这里在建攻击命令前计算一次（单击攻击，BeforeAttack 钩子不会改动消耗堆）。
    private decimal CalculatedDamage()
    {
        int souls = CombatState is not { } combatState || !combatState.IsLiveCombat()
            ? 0
            : Owner.PlayerCombatState?.ExhaustPile.Cards.Count(card => card is Soul) ?? 0;
        return CalculationBase + ExtraDamage * souls;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(CalculatedDamage()).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}
