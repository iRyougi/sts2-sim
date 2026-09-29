using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Delay</c>：2 费技能，获得 11（升级 13）格挡，下回合获得 1（升级 2）能量。</summary>
public sealed class Delay : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool GainsBlock => true;

    protected override int CanonicalEnergyCost => 2;

    private decimal Block => IsUpgraded ? 13m : 11m;

    private int Energy => IsUpgraded ? 2 : 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block, Energy: Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, Energy, Owner.Creature, this);
    }
}
