using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CloakAndDagger : CardModel
{
    public override bool GainsBlock => true;

    private int _cards = 1;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, 6m, ValueProp.Move, this, cardPlay);
        await Shiv.CreateInHand(Owner, _cards, CombatState!);
    }
    protected override void OnUpgrade() => _cards++;
}
