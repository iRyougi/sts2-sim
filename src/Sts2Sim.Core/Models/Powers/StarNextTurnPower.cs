using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>下回合获得等量星愿。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.StarNextTurnPower</c>）：
/// 复用能量重置的同一个回合开始时机触发，授予星愿，然后自我移除。</summary>
public sealed class StarNextTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        await PlayerCmd.GainStars(Amount, player);
        await PowerCmd.Remove(this);
    }
}
