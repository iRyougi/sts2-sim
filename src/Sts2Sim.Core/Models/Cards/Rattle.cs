using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Rattle</c>：Osty 对目标造成 7（升级 9）伤害，次数 = 1 + Osty 本回合已完成的攻击数
/// （在这次攻击开始前计算）；Osty 不在时无效果。</summary>
public sealed class Rattle : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 9m : 7m;

    private const int CalculationBase = 0;

    private const int CalculationExtra = 1;

    // OstyDamage 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    private int CalculatedHits => CalculationBase + CalculationExtra * (1 + (CombatState is CombatState state
        ? state.SemanticHistory.CountThisTurn(state, CombatSemanticHistory.ActorEvent.OstyAttack, Owner)
        : 0));

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
            .Targeting(cardPlay.Target).WithHitCount(CalculatedHits).Execute();
    }
}
