namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

public sealed class WasteAwayPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player.Creature == Owner ? Math.Max(0m, amount - Amount) : amount;
}
