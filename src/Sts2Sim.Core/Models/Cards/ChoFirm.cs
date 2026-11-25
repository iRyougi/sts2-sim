using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得格挡；若仍有可用箭矢则再获得一次（原版 <c>ChoFirm</c>）。</summary>
public sealed class ChoFirm : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Cho];
    private decimal Block => IsUpgraded ? 7m : 5m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override bool ShouldGlowGoldInternal => ArrowCmd.PlayerHasAnyArrows(Owner);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int gains = ArrowCmd.PlayerHasAnyArrows(Owner) ? 2 : 1;
        for (int i = 0; i < gains; i++)
        {
            await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        }
    }
}
