using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得格挡与 Aim（原版 <c>ChoReady</c>）。</summary>
public sealed class ChoReady : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Cho];
    private decimal Block => IsUpgraded ? 11m : 8m;
    private decimal AimAmount => IsUpgraded ? 5m : 3m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<AimPower>(CombatState!, Owner.Creature, AimAmount, Owner.Creature, this);
    }
}
