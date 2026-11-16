using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得格挡并翻倍当前 Stretch 层数（原版 <c>ChoKi</c>）。</summary>
public sealed class ChoKi : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Cho, CardTag.Ki];
    private decimal Block => IsUpgraded ? 11m : 8m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int stretchAmount = Owner.Creature.GetPower<StretchPower>()?.Amount ?? 0;
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        if (stretchAmount > 0)
        {
            await PowerCmd.Apply<StretchPower>(CombatState!, Owner.Creature, stretchAmount, Owner.Creature, this);
        }
    }
}
