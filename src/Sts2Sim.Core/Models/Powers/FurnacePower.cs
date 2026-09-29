using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>熔炉，自身回合开始时熔炼等于层数的伤害。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.FurnacePower</c>）。</summary>
public sealed class FurnacePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await ForgeCmd.Forge(Amount, Owner.Player!, this);
        }
    }
}
