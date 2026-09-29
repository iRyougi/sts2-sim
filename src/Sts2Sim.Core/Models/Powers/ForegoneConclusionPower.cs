using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>定局，摸牌前把等于层数的抽牌堆卡加入手牌，然后自我移除（一次性）。
/// 逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.ForegoneConclusionPower</c>），
/// 选择通过战斗 decision source 驱动。</summary>
public sealed class ForegoneConclusionPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        await CardPileCmd.ShuffleIfNecessary(Owner.CombatState!, player);
        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            Owner.CombatState!,
            player,
            player.PlayerCombatState!.DrawPile.Cards,
            Amount,
            Amount,
            this);
        foreach (CardModel card in selected)
        {
            CardPileCmd.Add(card, PileType.Hand);
        }

        await PowerCmd.Remove(this);
    }
}
