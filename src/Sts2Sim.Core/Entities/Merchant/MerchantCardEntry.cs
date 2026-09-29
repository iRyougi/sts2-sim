using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Entities.Merchant;

public sealed class MerchantCardEntry : MerchantEntry
{
    public CardModel Card { get; private set; }

    internal static CardModel ModifyCreatedCard(CardModel card, Player player)
    {
        card.AssignOwner(player);
        var cards = new List<CardModel> { card };
        Hook.ModifyMerchantCardCreationResults(player.RunState, player, cards);
        CardModel result = cards.Single();
        result.AssignOwner(player);
        return result;
    }

    internal void RefreshCardCreationResult(Player player) => Card = ModifyCreatedCard(Card, player);

    public MerchantCardEntry(CardModel card, int price, Player? player = null)
        : base(price, player)
    {
        Card = card;
    }

    /// <summary>Plan06a 偏离 #80/#81（商店无色/角色卡槽位缺内容）已在 Plan06b 铺开卡池后解决，
    /// 见 <see cref="MerchantInventory.GenerateCardEntries"/> 顶部注释。这条注释与那两个偏离无关，
    /// 说明的是折扣计算本身的一个独立细节：真实游戏 <c>MerchantCardEntry.SetOnSale</c> 用一次全新的
    /// 用一次全新的随机抽取（范围 0.95 到 1.05）重新计算价格再整除2，而不是直接对已生成的价格打五折——
    /// 逐字复刻这个消耗，保持 RNG 流的消耗顺序/次数与真实游戏一致。</summary>
    public static int CalculateDiscountedPrice(int basePrice, Rng shopRng) =>
        (int)Math.Round(basePrice * shopRng.NextFloat(0.95f, 1.05f)) / 2;
}
