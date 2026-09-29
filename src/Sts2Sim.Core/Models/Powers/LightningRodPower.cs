using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Powers;

public sealed class LightningRodPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player) return;
        await OrbCmd.Channel<LightningOrb>(Owner.CombatState!, player);
        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
    }
}
