namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

/// <summary>Blocks the next visible debuff applied to the owner, consuming one stack per block.</summary>
public sealed class ArtifactPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyPowerAmountReceived(
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? giver,
        out decimal modifiedAmount)
    {
        if (target != Owner || power.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            modifiedAmount = amount;
            return false;
        }

        modifiedAmount = 0m;
        return true;
    }

    public override Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        return PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, applier: null, cardSource: null);
    }
}
