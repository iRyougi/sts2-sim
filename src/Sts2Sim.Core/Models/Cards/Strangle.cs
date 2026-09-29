using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Strangle : CardModel, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 10m : 8m;
    private decimal StrangleAmount => IsUpgraded ? 3m : 2m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); await DamageCmd.Attack(Damage).FromCard(this, play).Targeting(play.Target).Execute(); await PowerCmd.Apply<StranglePower>(CombatState!, play.Target, StrangleAmount, Owner.Creature, this); }

}
