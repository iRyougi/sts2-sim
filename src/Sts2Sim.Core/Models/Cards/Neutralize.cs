using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Deals 3 damage, then applies 1 Weak; both values increase by 1 when upgraded.</summary>
public sealed class Neutralize : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 3m;
    private decimal _weak = 1m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override void OnUpgrade()
    {
        _damage += 1m;
        _weak += 1m;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
        await PowerCmd.Apply<WeakPower>(
            CombatState!,
            cardPlay.Target,
            _weak,
            Owner.Creature,
            this);
    }
}
