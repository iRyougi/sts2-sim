using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>PullFromBelow</c>：造成 5（升级 7）伤害 N 次，N = 本场战斗持有者打出完毕时带虚无的出牌次数
/// （原版 CardPlayFinishedEntry.WasEthereal，整场计数，不限本回合）。</summary>
public sealed class PullFromBelow : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private decimal Damage => IsUpgraded ? 7m : 5m;

    private const int CalculationBase = 0;

    private const int CalculationExtra = 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    private int CalculatedHits => CalculationBase + CalculationExtra * (CombatState is CombatState state
        ? state.SemanticHistory.CountThisCombat(state, CombatSemanticHistory.ActorEvent.EtherealPlayFinished, Owner)
        : 0);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).WithHitCount(CalculatedHits).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }
}
