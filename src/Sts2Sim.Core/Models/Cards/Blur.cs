using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Blur : CardModel
{
    public override bool GainsBlock => true;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, IsUpgraded ? 8m : 5m, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<BlurPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
}
