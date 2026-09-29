using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>DemesnePower</c>：持有者每回合抽手牌数 +层数，最大能量 +层数。</summary>
public sealed class DemesnePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal originalCardCount) =>
        player != Owner.Player ? originalCardCount : originalCardCount + Amount;

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player != Owner.Player ? amount : amount + Amount;
}
