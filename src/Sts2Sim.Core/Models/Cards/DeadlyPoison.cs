using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class DeadlyPoison : CardModel
{
    private decimal Poison => IsUpgraded ? 7m : 5m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); return PowerCmd.Apply<PoisonPower>(CombatState!, play.Target, Poison, Owner.Creature, this); }

}
