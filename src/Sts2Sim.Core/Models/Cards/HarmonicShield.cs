using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得 4（升级 6）点格挡，每种本回合已打出的旋律再加 4（升级 6）点（原版 <c>HarmonicShield</c>）。</summary>
public sealed class HarmonicShield : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal BaseBlock => IsUpgraded ? 6m : 4m;
    private decimal ExtraBlock => IsUpgraded ? 6m : 4m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal block = BaseBlock + ExtraBlock * MelodyCmd.MelodiesThisTurn(Owner);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, block, ValueProp.Move, this, cardPlay);
    }
}
