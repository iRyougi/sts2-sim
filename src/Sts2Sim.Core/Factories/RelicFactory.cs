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
    public static RelicModel PullNextRelicFromFront(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        RelicRarity rarity = RollRarity(player.PlayerRng.Rewards);
        // 偏离 #70：严格从所滚稀有度桶 front 抽取；空桶直接兜底 Circlet，不跨桶降级。
        return PullNextRelicFromFront(player, rarity);
    }

    public static RelicModel PullNextRelicFromFront(
        Player player, RelicRarity rarity, Predicate<RelicModel>? filter = null)
    {
        RelicModel relic = player.RelicGrabBag.PullFromFront(
            rarity, player.RunState, filter ?? (_ => true)) ?? ModelDb.Relic<Circlet>();
        RemoveFromSharedBag(player, relic);
        return relic;
    }

    public static RelicModel PullNextRelicFromBack(Player player, RelicRarity rarity)
    {
        RelicModel relic = player.RelicGrabBag.PullForShop(rarity, player.RunState)
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
