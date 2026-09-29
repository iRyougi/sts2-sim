using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs.Transplant;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// A one-shot draw pool partitioned by rarity. Common, Uncommon, Rare, and Shop relics are
/// independently shuffled into queues. Reward draws remove from the front, shop draws from the
/// back, and removed relics cannot reappear within the run.
/// 偏离 #70：省略真实游戏的多人 <c>_mpFallbackDequeue</c>，且某稀有度桶抽空时不做"自动降级找别档"
/// （真实游戏 <c>GetAvailableDeque</c> 会按 Shop→Common→Uncommon→Rare 顺序降级），直接返回 null。
/// Common、Uncommon、Rare、Shop 四档现在均有真实内容；保留严格分桶可让单档耗尽保持可观察，避免静默改变
/// 调用方请求的稀有度。单机场景不需要多人兜底，商店、精英奖励与宝箱继续显式以 Circlet 兜底。
/// </summary>
public sealed class RelicGrabBag
{
    private static readonly RelicRarity[] _bucketedRarities =
    {
        RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop,
    };

    private readonly Dictionary<RelicRarity, List<RelicModel>> _buckets = new();

    private RelicGrabBag(Dictionary<RelicRarity, List<RelicModel>> buckets)
    {
        _buckets = buckets;
    }

    public RelicGrabBag(Rng shuffleRng, IEnumerable<RelicModel> pool, bool includeAllRarities = false)
    {
        // Upstream inserts rarity buckets in the order first encountered in the source pool.
        // Preserve that order: each shuffle advances the shared initialization stream.
        foreach (IGrouping<RelicRarity, RelicModel> group in pool
                     .Where(relic => includeAllRarities || _bucketedRarities.Contains(relic.Rarity))
                     .GroupBy(relic => relic.Rarity))
        {
            List<RelicModel> bucket = group.ToList();
            shuffleRng.Shuffle(bucket);
            _buckets[group.Key] = bucket;
        }
    }

    public RelicModel? PullFromFront(RelicRarity rarity) => Pull(rarity, fromFront: true);

    public RelicModel? PullFromFront(RelicRarity rarity, IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        return Pull(rarity, fromFront: true, relic => relic.IsAllowed(runState));
    }

    public RelicModel? PullFromBack(RelicRarity rarity) => Pull(rarity, fromFront: false);

    /// <summary>Event-shop front draw; rejected relics remain available for other sources.</summary>
    public RelicModel? PullFromFront(RelicRarity rarity, IRunState runState, Predicate<RelicModel> predicate)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(predicate);
        return Pull(rarity, fromFront: true, relic => relic.IsAllowed(runState) && predicate(relic));
    }

    public bool HasAvailableRelics() => _bucketedRarities.Any(HasAvailableRelics);

    public bool HasAvailableRelics(RelicRarity rarity) =>
        _buckets.TryGetValue(rarity, out List<RelicModel>? bucket) && bucket.Count > 0;

    /// <summary>Remove every copy of an ID, including when passed a mutable owned relic.</summary>
    public void Remove(RelicModel relic)
    {
        ArgumentNullException.ThrowIfNull(relic);
        foreach (List<RelicModel> bucket in _buckets.Values)
            bucket.RemoveAll(candidate => candidate.Id == relic.Id);
    }

    internal RelicGrabBag Clone() => new(
        _buckets.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList()));

    internal RelicBagSnapshot ExportForTransplant() => new(
        _buckets.ToDictionary(
            pair => pair.Key.ToString(),
            pair => (IReadOnlyList<string>)pair.Value.Select(relic => relic.Id.ToString()).ToArray(),
            StringComparer.Ordinal));

    internal static RelicGrabBag ImportForTransplant(RelicBagSnapshot snapshot, string path)
    {
        if (snapshot?.Buckets is null)
            throw new TransplantMappingException(path, "Missing relic bag buckets.");
        var buckets = new Dictionary<RelicRarity, List<RelicModel>>();
        foreach ((string rarityName, IReadOnlyList<string> ids) in snapshot.Buckets)
        {
            if (!Enum.TryParse(rarityName, ignoreCase: false, out RelicRarity rarity) ||
                !Enum.IsDefined(rarity))
                throw new TransplantMappingException($"{path}.Buckets.{rarityName}", "Unknown rarity.");
            if (ids is null)
                throw new TransplantMappingException($"{path}.Buckets.{rarityName}", "Missing bucket.");
            var relics = new List<RelicModel>();
            for (int index = 0; index < ids.Count; index++)
            {
                string fieldPath = $"{path}.Buckets.{rarityName}[{index}]";
                if (string.IsNullOrWhiteSpace(ids[index]))
                    throw new TransplantMappingException(fieldPath, "Missing relic ID.");
                try
                {
                    relics.Add(ModelDb.GetById<RelicModel>(ModelId.Deserialize(ids[index])));
                }
                catch (Exception error) when (error is ArgumentException or System.Text.Json.JsonException or
                    Sts2Sim.Core.Models.Exceptions.ModelNotFoundException or InvalidCastException)
                {
                    throw new TransplantMappingException(fieldPath, error.Message);
                }
            }
            buckets.Add(rarity, relics);
        }
        return new RelicGrabBag(buckets);
    }
    /// <summary>Removes the last shop- and run-eligible relic while leaving ineligible relics in the bucket.</summary>
    public RelicModel? PullForShop(RelicRarity rarity, IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        return Pull(
            rarity,
            fromFront: false,
            relic => relic.IsAllowedInShops && relic.IsAllowed(runState));
    }

    private RelicModel? Pull(
        RelicRarity rarity,
        bool fromFront,
        Predicate<RelicModel>? predicate = null)
    {
        if (!_buckets.TryGetValue(rarity, out List<RelicModel>? bucket) || bucket.Count == 0)
        {
            return null;
        }

        int index = predicate is null
            ? fromFront ? 0 : bucket.Count - 1
            : fromFront ? bucket.FindIndex(predicate) : bucket.FindLastIndex(predicate);
        if (index < 0)
        {
            return null;
        }

        RelicModel relic = bucket[index];
        bucket.RemoveAt(index);
        return relic;
    }
}
