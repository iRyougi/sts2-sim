using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>任意敌方死亡时为己方存活玩家恢复能量（原版 <c>CompostPower</c>）。</summary>
public sealed class CompostPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDeath(Creature target, CardModel? source, bool wasRemovalPrevented)
    {
        if (target.Side == Owner.Side)
            return;

        IReadOnlyList<Creature> teammates = Owner.CombatState!.GetCreaturesOnSide(Owner.Side)
            .Where(c => c is not null && c.IsAlive && c.IsPlayer)
            .ToArray();
        foreach (Creature teammate in teammates)
        {
            await PlayerCmd.GainEnergy(Amount, teammate.Player!);
        }
    }
}
