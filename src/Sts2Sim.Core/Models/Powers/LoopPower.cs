using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class LoopPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner.Player || player.PlayerCombatState!.OrbQueue.Orbs.Count == 0) return;
        for (int i = 0; i < Amount; i++)
            await OrbCmd.Passive(Owner.CombatState!, player.PlayerCombatState.OrbQueue.Orbs[0]);
    }
}
