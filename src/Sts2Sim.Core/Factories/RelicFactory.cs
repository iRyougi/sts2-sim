using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Factories;

/// <summary>Draw relics and remove displayed candidates from the shared treasure bag.
/// Rarity odds are Common 50% / Uncommon 33% / Rare 17%, without Ascension adjustments.</summary>
public static class RelicFactory
{
    /// <summary>
    /// v0.113.1：用调用方给的 rng 掷稀有度后从 front 抽取。事件用自身 <c>Rng</c>（#946）。
    /// </summary>
    public static RelicModel PullNextRelicFromFront(Player player, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rng);
        return PullNextRelicFromFront(player, RollRarity(rng));
    }

    /// <summary>
    /// v0.113.1 由无 RNG 参数的 <c>PullNextRelicFromFront(Player)</c> 改名而来，隐式使用
    /// <c>PlayerRng.Rewards</c>；原版注明不得用于商店与事件。
    /// </summary>
    public static RelicModel PullNextRelicForRewardFromFront(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return PullNextRelicFromFront(player, RollRarityForReward(player));
    }

    public static RelicRarity RollRarityForReward(Player player) => RollRarity(player.PlayerRng.Rewards);

    /// <summary>原生 <c>PullNextRelicFromFront(player, rarity, filter)</c>：袋内按 <c>GetAvailableDeque</c> 剔除、降级（#1120），
    /// 都没有才给 <c>FallbackRelic</c>（Circlet）。</summary>
    public static RelicModel PullNextRelicFromFront(
        Player player, RelicRarity rarity, Predicate<RelicModel>? filter = null)
    {
        RelicModel relic = player.RelicGrabBag.PullFromFront(
            rarity, player.RunState, filter ?? (_ => true)) ?? ModelDb.Relic<Circlet>();
        RemoveFromSharedBag(player, relic);
        return relic;
    }

    /// <summary>原生 <c>MerchantRelicEntry.FillSlot</c> → <c>PullNextRelicFromBack(player, rarity, r =&gt; r.IsAllowedInShops)</c>；
    /// 降级时同样只看可上架的遗物（#1120）。原生补货（<c>RestockAfterPurchase</c>）另传当前货架的 blacklist，
    /// 单人局等价：货架上的遗物已从袋中取出，blacklist 匹配不到袋内任何遗物。</summary>
    public static RelicModel PullNextRelicFromBack(Player player, RelicRarity rarity)
    {
        RelicModel relic = player.RelicGrabBag.PullFromBack(
                rarity, static candidate => candidate.IsAllowedInShops, player.RunState)
            ?? ModelDb.Relic<Circlet>();
        RemoveFromSharedBag(player, relic);
        return relic;
    }

    internal static void RemoveFromSharedBag(Player player, RelicModel relic)
    {
        if (player.RunState is RunState run)
            run.SharedRelicGrabBag?.Remove(relic);
    }

    public static RelicRarity RollRarity(Rng rng)
    {
        float roll = rng.NextFloat();
        if (roll < 0.5f)
        {
            return RelicRarity.Common;
        }
        return roll < 0.83f ? RelicRarity.Uncommon : RelicRarity.Rare;
    }
}
