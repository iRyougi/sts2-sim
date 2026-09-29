using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Grants Strength equal to its amount when the owner's side ends its turn.</summary>
public sealed class TerritorialPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, Amount, Owner, null);
        }
    }
}
