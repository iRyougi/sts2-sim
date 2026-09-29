using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Official source contains only existing block and Weak primitives.</summary>
public sealed class LegSweep : CardModel
{
    public override bool GainsBlock => true;

    private decimal Block => IsUpgraded ? 14m : 11m;
    private decimal Weak => IsUpgraded ? 3m : 2m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, play); await PowerCmd.Apply<WeakPower>(CombatState!, play.Target, Weak, Owner.Creature, this); }

}
