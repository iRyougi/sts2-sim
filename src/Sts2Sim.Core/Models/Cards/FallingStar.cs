using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Regent starter attack. Deals 8 damage and applies one Weak and one Vulnerable.
/// </summary>
public sealed class FallingStar : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 8m;
    private const decimal WeakAmount = 1m;
    private const decimal VulnerableAmount = 1m;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override int CanonicalStarCost => 2;

    protected override void OnUpgrade() => _damage += 4m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, WeakAmount, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, VulnerableAmount, Owner.Creature, this);
    }
}
