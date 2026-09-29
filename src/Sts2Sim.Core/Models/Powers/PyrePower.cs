using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class PyrePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        ReferenceEquals(player, Owner.Player) ? amount + Amount : amount;
}
