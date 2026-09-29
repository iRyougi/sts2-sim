using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Grant stored unpowered block when the owner's block is next cleared.</summary>
public sealed class SelfFormingClayPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (!ReferenceEquals(creature, Owner))
            return;
        await CreatureCmd.GainBlock(Owner.CombatState!, Owner,
            Amount, ValueProp.Unpowered, null, null);
        await PowerCmd.Remove(this);
    }
}
