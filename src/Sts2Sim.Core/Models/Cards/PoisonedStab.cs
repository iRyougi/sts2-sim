using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class PoisonedStab : CardModel, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 8m : 6m;
    private decimal Poison => IsUpgraded ? 4m : 3m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); await DamageCmd.Attack(Damage).FromCard(this, play).Targeting(play.Target).Execute(); await PowerCmd.Apply<PoisonPower>(CombatState!, play.Target, Poison, Owner.Creature, this); }

}
