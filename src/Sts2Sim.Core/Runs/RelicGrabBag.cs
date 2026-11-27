using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs.Transplant;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// A one-shot draw pool partitioned by rarity. Common, Uncommon, Rare, and Shop relics are
/// independently shuffled into queues. Reward draws remove from the front, shop draws from the
/// back, and removed relics cannot reappear within the run.
/// 带 <see cref="IRunState"/> 的取用入口逐字移植原生 <c>RelicGrabBag.GetAvailableDeque</c>（#1120，v0.111.0–v0.114.0
/// 三版相同）：先永久剔除不允许的遗物；请求档为空且允许 refresh 时按原始顺序补回（不耗 RNG）；仍无符合过滤器的
/// 遗物就按 Shop→Common→Uncommon→Rare 降级；都没有返回 null，由调用方以 Circlet 兜底。
/// 单人化简（见偏离登记册 #1120 节）：不移植多人共享宝箱专用的 <c>_mpFallbackDequeue</c>/<c>MoveToFallback</c>（单人恒空）；
/// 原生共享袋 <c>refreshAllowed = players.Count &gt; 1</c>，模拟器只跑单人局，构造参数默认 false。
/// </summary>
public sealed class RelicGrabBag
{
    private static readonly RelicRarity[] _bucketedRarities =
    {
        RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop,
    };

    private Dictionary<RelicRarity, List<RelicModel>> _buckets = new();
    private bool _sharedBuckets;

    /// <summary>原生 <c>_refreshAllowed</c>：玩家袋恒 false；读档（Transplant 导入）恒 false。</summary>
    private readonly bool _refreshAllowed;

    /// <summary>原生 <c>_originalRelics</c>：构造时过滤后的原始池（未洗牌顺序），只读共享；读档为 null。</summary>
    private readonly IReadOnlyList<RelicModel>? _originalRelics;

    private RelicGrabBag(
        Dictionary<RelicRarity, List<RelicModel>> buckets,
        bool refreshAllowed,
        IReadOnlyList<RelicModel>? originalRelics,
        bool sharedBuckets = false)
    {
        _buckets = buckets;
        _refreshAllowed = refreshAllowed;
        _originalRelics = originalRelics;
        _sharedBuckets = sharedBuckets;
    }

    public RelicGrabBag(
        Rng shuffleRng, IEnumerable<RelicModel> pool, bool includeAllRarities = false, bool refreshAllowed = false)
    {
        // Upstream inserts rarity buckets in the order first encountered in the source pool.
        // Preserve that order: each shuffle advances the shared initialization stream.
        RelicModel[] original = pool
            .Where(relic => includeAllRarities || _bucketedRarities.Contains(relic.Rarity))
            .ToArray();
        foreach (IGrouping<RelicRarity, RelicModel> group in original.GroupBy(relic => relic.Rarity))
        {
            List<RelicModel> bucket = group.ToList();
            bucket.UnstableShuffle(shuffleRng);
            _buckets[group.Key] = bucket;
        }
        _refreshAllowed = refreshAllowed;
        _originalRelics = Array.AsReadOnly(original);
    }

    /// <summary>严格单桶的测试/诊断入口：不剔除、不降级。生产路径一律用带 <see cref="IRunState"/> 的重载。</summary>
    public RelicModel? PullFromFront(RelicRarity rarity) => PullStrict(rarity, fromFront: true);

    /// <summary>原生 <c>PullFromFront(rarity, runState)</c>。</summary>
    public RelicModel? PullFromFront(RelicRarity rarity, IRunState runState) =>
        PullFromFront(rarity, runState, static _ => true);

    /// <summary>严格单桶的测试/诊断入口：不剔除、不降级。生产路径一律用带 <see cref="IRunState"/> 的重载。</summary>
    public RelicModel? PullFromBack(RelicRarity rarity) => PullStrict(rarity, fromFront: false);

    /// <summary>原生 <c>PullFromFront(rarity, filter, runState)</c>：在可用桶里取第一件符合过滤器的遗物。</summary>
    public RelicModel? PullFromFront(RelicRarity rarity, IRunState runState, Predicate<RelicModel> filter)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(filter);
        List<RelicModel>? deque = GetAvailableDeque(rarity, runState, filter);
        if (deque is null || deque.Count == 0)
            return null;
        for (int i = 0; i < deque.Count; i++)
        {
            RelicModel relic = deque[i];
            if (filter(relic))
            {
                deque.RemoveAt(i);
                return relic;
            }
        }
        return null;
    }

    /// <summary>原生 <c>PullFromBack(rarity, filter, runState)</c>（商店）：在可用桶里取最后一件符合过滤器的遗物。</summary>
    public RelicModel? PullFromBack(RelicRarity rarity, Predicate<RelicModel> filter, IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(runState);
        List<RelicModel>? deque = GetAvailableDeque(rarity, runState, filter);
        if (deque is null || deque.Count == 0)
            return null;
        for (int i = deque.Count - 1; i >= 0; i--)
        {
            RelicModel relic = deque[i];
            if (filter(relic))
            {
                deque.RemoveAt(i);
                return relic;
            }
        }
        return null;
    }

    /// <summary>原生 <c>HasAvailableRelics(runState)</c>：与原生一样会剔除不允许的遗物（及 refresh 副作用）。</summary>
    public bool HasAvailableRelics(IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        foreach (RelicRarity rarity in _bucketedRarities)
        {
            if (GetAvailableDeque(rarity, runState, static _ => true) is not null)
                return true;
        }
        return false;
    }

    /// <summary>严格单桶计数的测试/诊断入口；生产路径用 <see cref="HasAvailableRelics(IRunState)"/>。</summary>
    public bool HasAvailableRelics()
    {
        for (int i = 0; i < _bucketedRarities.Length; i++)
        {
            if (HasAvailableRelics(_bucketedRarities[i]))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>严格单桶计数的测试/诊断入口。</summary>
    public bool HasAvailableRelics(RelicRarity rarity) =>
        _buckets.TryGetValue(rarity, out List<RelicModel>? bucket) && bucket.Count > 0;

    /// <summary>Remove every copy of an ID, including when passed a mutable owned relic.</summary>
    public void Remove(RelicModel relic)
    {
        ArgumentNullException.ThrowIfNull(relic);
        EnsureWritable();
        foreach (List<RelicModel> bucket in _buckets.Values)
            bucket.RemoveAll(candidate => candidate.Id == relic.Id);
    }

    private void EnsureWritable()
    {
        if (!_sharedBuckets)
        {
            return;
        }

        _buckets = CopyBuckets(_buckets);
        _sharedBuckets = false;
    }

    private static Dictionary<RelicRarity, List<RelicModel>> CopyBuckets(
        Dictionary<RelicRarity, List<RelicModel>> source)
    {
        var copy = new Dictionary<RelicRarity, List<RelicModel>>(source.Count);
        foreach ((RelicRarity rarity, List<RelicModel> bucket) in source)
        {
            copy.Add(rarity, new List<RelicModel>(bucket));
        }
        return copy;
    }

    internal RelicGrabBag Clone() => new(
        CopyBuckets(_buckets),
        _refreshAllowed,
        _originalRelics);

    /// <summary>Combat-projection copy-on-write clone: shares buckets until either side mutates.</summary>
    internal RelicGrabBag CloneSharedForCombat()
    {
        _sharedBuckets = true;
        return new RelicGrabBag(_buckets, _refreshAllowed, _originalRelics, sharedBuckets: true);
    }

    internal RelicGrabBag CloneReshuffled(Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        RelicRarity[] rarities = _buckets.Keys.ToArray();
        Array.Sort(rarities);
        var buckets = new Dictionary<RelicRarity, List<RelicModel>>(rarities.Length);
        foreach (RelicRarity rarity in rarities)
        {
            var bucket = new List<RelicModel>(_buckets[rarity]);
            bucket.Sort(static (left, right) =>
                StringComparer.Ordinal.Compare(left.Id.Entry, right.Id.Entry));
            bucket.UnstableShuffle(rng);
            buckets.Add(rarity, bucket);
        }
        return new RelicGrabBag(buckets, _refreshAllowed, _originalRelics);
    }

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
        // 对应原生 FromSerializable：无参构造，refresh 关闭、无原始池。
        return new RelicGrabBag(buckets, refreshAllowed: false, originalRelics: null);
    }
    private RelicModel? PullStrict(RelicRarity rarity, bool fromFront)
    {
        if (!_buckets.TryGetValue(rarity, out List<RelicModel>? bucket) || bucket.Count == 0)
            return null;
        EnsureWritable();
        bucket = _buckets[rarity];
        int index = fromFront ? 0 : bucket.Count - 1;
        RelicModel relic = bucket[index];
        bucket.RemoveAt(index);
        return relic;
    }

    /// <summary>原生 <c>GetAvailableDeque</c> 逐字移植。</summary>
    private List<RelicModel>? GetAvailableDeque(RelicRarity rarity, IRunState runState, Predicate<RelicModel> filter)
    {
        EnsureWritable();
        RemoveDisallowedRelicsFromDeques(runState);
        List<RelicModel>? deque = GetDeque(rarity);
        if (deque.Count == 0 && _refreshAllowed)
        {
            RefreshRarity(rarity);
            RemoveDisallowedRelicsFromDeques(runState);
        }
        while (deque is not null && !deque.Exists(filter))
        {
            rarity = rarity switch
            {
                RelicRarity.Shop => RelicRarity.Common,
                RelicRarity.Common => RelicRarity.Uncommon,
                RelicRarity.Uncommon => RelicRarity.Rare,
                _ => RelicRarity.None,
            };
            deque = rarity == RelicRarity.None ? null : GetDeque(rarity);
        }
        // 原生此处回退到多人 _mpFallbackDequeue；单人恒空，不移植。
        return deque;
    }

    private void RemoveDisallowedRelicsFromDeques(IRunState runState)
    {
        foreach (List<RelicModel> bucket in _buckets.Values)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                if (!bucket[i].IsAllowed(runState))
                {
                    bucket.RemoveAt(i);
                    i--;
                }
            }
        }
    }

    /// <summary>原生 <c>GetDeque</c>：不存在的桶返回一个不入字典的新空表。</summary>
    private List<RelicModel> GetDeque(RelicRarity rarity) =>
        _buckets.TryGetValue(rarity, out List<RelicModel>? bucket) ? bucket : new List<RelicModel>();

    private void RefreshRarity(RelicRarity rarity)
    {
        if (_originalRelics is null)
            throw new InvalidOperationException("Tried to refresh relics but original list is null");
        foreach (RelicModel original in _originalRelics)
        {
            if (original.Rarity != rarity)
                continue;
            if (!_buckets.TryGetValue(original.Rarity, out List<RelicModel>? bucket))
            {
                bucket = new List<RelicModel>();
                _buckets[original.Rarity] = bucket;
            }
            bucket.Add(original);
        }
    }
}
