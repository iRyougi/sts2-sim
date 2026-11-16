using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>回合开始获得 Stretch（原版 <c>HiddenRootsPower</c>）。</summary>
public sealed class HiddenRootsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Apply<StretchPower>(Owner.CombatState!, Owner, Amount, Owner, null);
        }
    }
}
