using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class DefendDefect : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 5m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Basic;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Defend];
    protected override Task OnPlay(CardPlay cardPlay) =>
        CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
    protected override void OnUpgrade() => _block += 3m;
}
