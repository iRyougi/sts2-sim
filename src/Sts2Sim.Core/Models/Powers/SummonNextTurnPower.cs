using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>SummonNextTurnPower</c>：持有者的下个玩家回合开始时召唤层数，然后移除。</summary>
public sealed class SummonNextTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner.Player || AmountOnTurnStart == 0)
            return;

        await OstyCmd.Summon(player, Amount, this);
        await PowerCmd.Remove(this);
    }
}
