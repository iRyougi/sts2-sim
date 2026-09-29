using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class HammerTimePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterForge(decimal amount, Player forger, AbstractModel? source)
    {
        if (source is HammerTimePower || forger.Creature != Owner)
        {
            return;
        }

        foreach (Player teammate in Owner.CombatState!.Players
                     .Where(player => player != forger && player.Creature.IsAlive))
        {
            await ForgeCmd.Forge(amount, teammate, this);
        }
    }
}
