using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>PullAggro</c>：召唤 4（升级 5），再获得 7（升级 9）格挡。</summary>
public sealed class PullAggro : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    public override bool GainsBlock => true;

    protected override int CanonicalEnergyCost => 2;

    private decimal Summon => IsUpgraded ? 5m : 4m;

    private decimal Block => IsUpgraded ? 9m : 7m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await OstyCmd.Summon(Owner, Summon, this);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
    }
}
