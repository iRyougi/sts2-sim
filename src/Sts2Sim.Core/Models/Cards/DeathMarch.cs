using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>DeathMarch</c>：1 费攻击，造成 8（升级 9）+ 本回合持有者在抽牌阶段以外抽到的牌数 × 4（升级 6）。</summary>
public sealed class DeathMarch : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private decimal CalculationBase => IsUpgraded ? 9m : 8m;

    private decimal ExtraDamage => IsUpgraded ? 6m : 4m;

    private int CardsDrawnOutsideHandDrawThisTurn =>
        CombatState is CombatState concrete
            ? concrete.SemanticHistory.CountCardsDrawnOutsideHandDrawThisTurn(concrete, Owner)
            : 0;

    private decimal CalculatedDamage => CalculationBase + ExtraDamage * CardsDrawnOutsideHandDrawThisTurn;

    // 只有 CalculationBase/ExtraDamage/CalculatedDamage，没有字面 Damage 键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CalculatedDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(CalculatedDamage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}
