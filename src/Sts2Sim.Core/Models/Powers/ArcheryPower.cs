using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>回合开始获得 Aim（原版 <c>ArcheryPower</c>）。</summary>
public sealed class ArcheryPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Apply<AimPower>(Owner.CombatState!, Owner, Amount, Owner, null);
        }
    }
}
