using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Powers;

public sealed class SpinnerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player) return;
        for (int i = 0; i < Amount; i++)
            await OrbCmd.Channel<GlassOrb>(Owner.CombatState!, player);
    }
}
