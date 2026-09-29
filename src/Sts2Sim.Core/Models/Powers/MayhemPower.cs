using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Commands;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>混乱，在持有者的 AutoPrePlay 阶段自动打出抽牌堆顶部卡。</summary>
public sealed class MayhemPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterAutoPrePlayPhaseEntered(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        await AutoPlayCmd.FromTopOfDrawPile(Owner.CombatState!, Owner.Player!, (int)Amount);
    }
}
