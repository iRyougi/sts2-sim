using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class RadiancePower : PowerModel
{
    private const decimal EnergyPerReset = 1m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        player.PlayerCombatState!.GainEnergy(EnergyPerReset);
        await PowerCmd.TickDownDuration(Owner.CombatState!, this);
    }
}
