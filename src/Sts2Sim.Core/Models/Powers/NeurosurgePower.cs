using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>NeurosurgePower</c>：持有者一方每个回合开始时，给持有者施加层数的 <see cref="DoomPower"/>。</summary>
public sealed class NeurosurgePower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
            await PowerCmd.Apply<DoomPower>(Owner.CombatState!, Owner, Amount, Owner, null);
    }
}
