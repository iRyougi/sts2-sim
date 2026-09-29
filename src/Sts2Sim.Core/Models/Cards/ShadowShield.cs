using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ShadowShield : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 11m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        await OrbCmd.Channel<DarkOrb>(CombatState!, Owner);
    }

    protected override void OnUpgrade() => _block += 4m;
}
