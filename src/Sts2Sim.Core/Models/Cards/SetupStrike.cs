using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class SetupStrike : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 7m;
    private decimal _strength = 3m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await PowerCmd.Apply<SetupStrikePower>(CombatState!, Owner.Creature, _strength,
            Owner.Creature, this);
    }

    protected override void OnUpgrade() => UpgradeValues();

    private void UpgradeValues()
    {
        _damage += 2m;
        _strength += 1m;
    }
}
