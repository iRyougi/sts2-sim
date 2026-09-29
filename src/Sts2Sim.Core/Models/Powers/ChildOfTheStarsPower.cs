using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ChildOfTheStarsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterStarsSpent(int amount, Player spender)
    {
        if (amount <= 0 || spender.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(
            Owner.CombatState!,
            Owner,
            Amount * amount,
            ValueProp.Unpowered,
            null,
            null);
    }
}
