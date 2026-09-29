using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Dodge and Roll: current block, then equal next-turn block.</summary>
public sealed class DodgeAndRoll : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 4m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal gained = await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<BlockNextTurnPower>(CombatState!, Owner.Creature, gained, Owner.Creature, this);
    }
    protected override void OnUpgrade() => _block += 2m;
}
