using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BubbleBubble : CardModel
{
    private decimal Poison => IsUpgraded ? 12m : 9m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); if (play.Target.HasPower<PoisonPower>()) await PowerCmd.Apply<PoisonPower>(CombatState!, play.Target, Poison, Owner.Creature, this); }

}
