namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;

public sealed class StockPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDeath(Creature target)
    {
        if (!ReferenceEquals(target, Owner) || Amount <= 0 || Owner.CombatState is null)
        {
            return;
        }

        var replacement = (Axebot)ModelDb.Monster<Axebot>().MutableClone();
        replacement.StockAmount = Amount - 1;
        await CreatureCmd.Add(replacement, Owner.CombatState, Owner.Side, Owner.SlotName);
    }

    public override bool ShouldStopCombatFromEnding() => Amount > 0;
}
