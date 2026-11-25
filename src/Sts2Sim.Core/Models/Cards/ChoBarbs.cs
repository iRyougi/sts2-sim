using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得格挡并获得临时荆棘（原版 <c>ChoBarbs</c>）。</summary>
public sealed class ChoBarbs : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Cho];
    private decimal Block => IsUpgraded ? 10m : 8m;
    private decimal ThornsAmount => IsUpgraded ? 3m : 2m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<ChoBarbsPower>(CombatState!, Owner.Creature, ThornsAmount, Owner.Creature, this);
    }
}
