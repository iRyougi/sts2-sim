using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class FeedingFrenzy : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<FeedingFrenzyPower>(CombatState!, Owner.Creature, IsUpgraded ? 7m : 5m, Owner.Creature, this);
}
