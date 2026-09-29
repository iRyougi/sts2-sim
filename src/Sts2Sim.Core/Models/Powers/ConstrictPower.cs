namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

/// <summary>Damages the owner at the end of its side's turn and ends when the applier dies.</summary>
public sealed class ConstrictPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await CreatureCmd.Damage(
                Owner.CombatState!,
                new[] { Owner },
                Amount,
                ValueProp.Unpowered,
                dealer: Owner,
                cardSource: null,
                cardPlay: null);
        }
    }

    public override async Task AfterDeath(Creature target)
    {
        if (target == Applier && Owner.Powers.Contains(this))
        {
            await PowerCmd.Remove(this);
        }
    }
}
