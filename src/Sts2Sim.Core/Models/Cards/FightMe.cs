using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class FightMe : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    private decimal _damage = 5m;
    private decimal _strength = 3m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).WithHitCount(2)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await PowerCmd.Apply<StrengthPower>(CombatState!, Owner.Creature, _strength, Owner.Creature, this);
        await PowerCmd.Apply<StrengthPower>(CombatState!, cardPlay.Target, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        _damage += 1m;
        _strength += 1m;
    }
}
