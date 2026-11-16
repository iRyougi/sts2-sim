using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>下回合额外摸N张牌。逐字移植核心行为（<c>MegaCrit.Sts2.Core.Models.Powers.DrawCardsNextTurnPower</c>）：
/// 通过 <see cref="AmountOnTurnStart"/> 跨回合快照防重复——只有在"回合开始前就已持有"的层数才会加成摸牌,
/// 本回合中途新施加的层数要等到下一次回合开始快照后才生效,v0.113.1 起改为在摸牌后经 <see cref="AfterModifyingHandDraw"/> 移除（仅当本能力确实改变了摸牌数）。#95 曾以为
/// 这层快照可以省略（"唯一生成源 Glow 总在战斗中段出牌，不会和摸牌那一刻精确重合”），但现在生成源已有
/// 5 个（Glow、Plot、Predator、Relax、PaleBlueDotPower），其中不乏可能在摸牌前后连续触发的场景，因此
/// 按上游语义把 <see cref="AmountOnTurnStart"/>（由 <c>Creature.BeforeTurnStart</c> 维护）接上，不再省略。</summary>
public sealed class DrawCardsNextTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player) return count;
        if (AmountOnTurnStart == 0) return count;
        return count + Amount;
    }

    // v0.113.1：只有本能力确实改变了手牌摸牌数（Hook.ModifyHandDraw 把它列为 modifier）才在摸牌后移除。
    public override async Task AfterModifyingHandDraw()
    {
        await PowerCmd.Remove(this);
    }
}
