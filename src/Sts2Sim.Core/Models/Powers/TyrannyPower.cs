using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>暴政，多抽等于层数的牌，但每回合开始耗尽手牌里等于层数的卡。
/// 逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.TyrannyPower</c>）。真实源码的 `AfterPlayerTurnStart` 是"这名玩家的回合开始"，
/// 在普通玩家回合开始阶段执行，早于 AfterPlayerTurnStartLate。</summary>
public sealed class TyrannyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal originalCardCount) =>
        player == Owner.Player ? originalCardCount + Amount : originalCardCount;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            Owner.CombatState!,
            Owner.Player!,
            Owner.Player!.PlayerCombatState!.Hand.Cards,
            Amount,
            Amount,
            this);
        foreach (CardModel card in selected)
        {
            await CardPileCmd.Exhaust(Owner.CombatState!, card);
        }
    }
}
