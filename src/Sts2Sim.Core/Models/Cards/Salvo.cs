using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>12伤害,1层"保留手牌"。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Salvo</c>）。</summary>
public sealed class Salvo : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 12m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await PowerCmd.Apply<RetainHandPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _damage += 4m;
}
