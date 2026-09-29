using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Official source contains only existing damage and Weak primitives.</summary>
public sealed class SuckerPunch : CardModel, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 10m : 8m;
    private decimal Weak => IsUpgraded ? 2m : 1m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); await DamageCmd.Attack(Damage).FromCard(this, play).Targeting(play.Target).Execute(); await PowerCmd.Apply<WeakPower>(CombatState!, play.Target, Weak, Owner.Creature, this); }

}
