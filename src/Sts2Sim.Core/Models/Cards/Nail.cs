using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Nail : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 25m : 20m;
    private decimal Power => IsUpgraded ? 3m : 2m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            if (CombatState == null)
            {
                return false;
            }
            return CombatState.HittableEnemies.Any(ArrowCmd.CreatureIsImpaledByArrow);
        }
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (ArrowCmd.CreatureIsImpaledByArrow(cardPlay.Target))
        {
            await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
            await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, Power, Owner.Creature, this);
            await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, Power, Owner.Creature, this);
        }
    }
}
