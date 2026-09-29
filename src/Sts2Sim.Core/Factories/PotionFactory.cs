using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Factories;

/// <summary>Creates potion candidates with the standard rarity odds.</summary>
public static class PotionFactory
{
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
    }

    public static Rarity RollRarity(Rng rng)
    {
        float roll = rng.NextFloat();
        if (roll <= 0.1f)
        {
            return Rarity.Rare;
        }

        return roll <= 0.35f ? Rarity.Uncommon : Rarity.Common;
    }

    public static PotionModel? CreateRandom(Rarity rarity, Rng rng) =>
        CreateRandom(rarity, rng, static _ => true);

    public static PotionModel? CreateRandomForCombat(Rarity rarity, Rng rng) =>
        CreateRandom(rarity, rng, static potion => potion.CanBeGeneratedInCombat);

    public static PotionModel? CreateRandomForCombat(Player player, Rarity rarity, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rng);
        return CreateRandomFromPool(rarity, rng,
            GetOutOfCombatPool(player).Where(potion => potion.CanBeGeneratedInCombat));
    }

    /// <summary>Creates the standard out-of-combat potion pickup using the caller-owned RNG stream.</summary>
    public static PotionModel? CreateRandomOutOfCombat(Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return CreateRandom(RollRarity(rng), rng);
    }

    public static PotionModel? CreateRandomOutOfCombat(Player player, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rng);
        return CreateRandomFromPool(RollRarity(rng), rng, GetOutOfCombatPool(player));
    }

    /// <summary>出战斗池抽取，并额外过滤（例如保证同一批次内不重复）。
    /// 偏离 #306（2026-09-08）：<c>PhialHolster</c> 原先走扁平的 <c>CreateRandom</c>，
    /// 与 <c>Wellspring</c>（偏离 #301）同源。</summary>
    public static PotionModel? CreateRandomOutOfCombat(
        Player player,
        Rng rng,
        Func<PotionModel, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(predicate);
        return CreateRandomFromPool(RollRarity(rng), rng, GetOutOfCombatPool(player).Where(predicate));
    }

    public static IReadOnlyList<PotionModel> GetOutOfCombatPool(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Character.PotionPool.GetUnlockedPotions(player.UnlockState)
            .Concat(SharedPotionPool.Instance.GetUnlockedPotions(player.UnlockState))
            .DistinctBy(potion => potion.Id)
            .ToArray();
    }

    internal static PotionModel? CreateRandom(
        Rarity rarity,
        Rng rng,
        Func<PotionModel, bool> predicate)
    {
        return CreateRandomFromPool(rarity, rng, ModelDb.All<PotionModel>().Where(predicate));
    }

    private static PotionModel? CreateRandomFromPool(
        Rarity rarity,
        Rng rng,
        IEnumerable<PotionModel> orderedPool)
    {
        PotionRarity targetRarity = rarity switch
        {
            Rarity.Common => PotionRarity.Common,
            Rarity.Uncommon => PotionRarity.Uncommon,
            Rarity.Rare => PotionRarity.Rare,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
        };

        List<PotionModel> pool = orderedPool
            .Where(potion => potion.Rarity == targetRarity)
            .ToList();

        PotionModel? canonical = rng.NextItem(pool);
        return canonical is null ? null : (PotionModel)canonical.MutableClone();
    }
}
