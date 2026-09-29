using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Tracking : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override Task OnPlay(CardPlay play) => PowerCmd.Apply<TrackingPower>(CombatState!, Owner.Creature, 50m, Owner.Creature, this);
    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
