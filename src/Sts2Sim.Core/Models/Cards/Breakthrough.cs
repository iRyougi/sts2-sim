using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Breakthrough : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    private decimal _damage = 9m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.Damage(CombatState!, [Owner.Creature], 1m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature, this, cardPlay);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
    }

    protected override void OnUpgrade() => _damage += 4m;
}
