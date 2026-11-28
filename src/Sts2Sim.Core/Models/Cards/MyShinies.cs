using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>v0.114.0 colorless rare (#1110): 2 damage per hit, hits = (int)(1 + Gold / 25); upgrade adds 1 damage
/// (v0.113.1 was 1 damage, hits = Gold / 20, upgrade lowered the divisor to 10). Outside combat the native multiplier
/// term is 0, so the hit count is the CalculationBase. 偏离 #148：Gold 与 CalculationBase/Extra 用字段。</summary>
public sealed class MyShinies : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const decimal CalculationBase = 1m;
    private const decimal CalculationExtra = 1m;
    private decimal _damage = 2m;
    private decimal _goldPerHit = 25m;

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
    public override bool IsAllowedInBlitz => false;
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

    protected override void OnUpgrade() => _damage += 1m;
}
