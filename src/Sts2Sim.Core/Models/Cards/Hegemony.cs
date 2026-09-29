using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>15伤害,2层下回合能量。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Hegemony</c>）。</summary>
public sealed class Hegemony : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 15m;
    private decimal _nextTurnEnergy = 2m;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, _nextTurnEnergy, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        _damage += 3m;
        _nextTurnEnergy += 1m;
    }
}
