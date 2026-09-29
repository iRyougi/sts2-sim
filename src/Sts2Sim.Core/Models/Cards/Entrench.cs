using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Entrench : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 2;
    protected override void OnUpgrade() => ReduceEnergyCost(1);

    protected override Task OnPlay(CardPlay cardPlay) =>
        CreatureCmd.GainBlock(CombatState!, Owner.Creature, Owner.Creature.Block,
            ValueProp.Unpowered | ValueProp.Move, this, cardPlay);
}
