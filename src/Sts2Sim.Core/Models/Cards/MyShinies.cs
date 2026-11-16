using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>v0.113.1 colorless rare (#927): 1 damage per hit, hits = (int)(Gold / GoldPerHit); upgrade lowers the
/// divisor from 20 to 10. Native CalculatedVar is 0 outside combat. 偏离 #148：Gold 与 CalculationBase/Extra 用字段。</summary>
public sealed class MyShinies : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const decimal CalculationBase = 0m;
    private const decimal CalculationExtra = 1m;
    private decimal _damage = 1m;
    private decimal _goldPerHit = 20m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override bool IsColorless => true;
    protected override int CanonicalEnergyCost => 1;

    public decimal GoldPerHit => _goldPerHit;

    public int CalculatedHits => (int)(CalculationBase + CalculationExtra *
        (CombatState is not null ? Owner.Gold / _goldPerHit : 0m));

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).WithHitCount(CalculatedHits).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _goldPerHit -= 10m;
}
