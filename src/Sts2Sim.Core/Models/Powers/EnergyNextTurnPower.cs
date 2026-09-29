using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>下回合获得等量能量。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.EnergyNextTurnPower</c>）：
/// 在自己一方回合开始能量重置之后触发，授予能量，然后自我移除。偏离 #94：直接调用
/// <c>PlayerCombatState.GainEnergy</c>（本项目没有单独的 <c>PlayerCmd.GainEnergy</c> 包装,
/// 与 <c>GeneratedCardModel</c> 现有的能量授予方式保持一致）。</summary>
public sealed class EnergyNextTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        player.PlayerCombatState!.GainEnergy(Amount);
        await PowerCmd.Remove(this);
    }
}
