using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>ForbiddenGrimoirePower</c>：战斗结束时，每层给持有者追加一个删牌奖励。</summary>
public sealed class ForbiddenGrimoirePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterCombatEnd()
    {
        // 投影里不得触碰真实 CombatRoom（见 HeistPower/SwipePower）。
        if ((Owner.CombatState as CombatState)?.IsProjection == true ||
            Owner.Player is not Player player ||
            Owner.CombatState?.RunState.CurrentRoom is not CombatRoom room)
            return Task.CompletedTask;

        for (int i = 0; i < Amount; i++)
            room.AddExtraReward(player, new CardRemovalReward(player));

        return Task.CompletedTask;
    }
}
