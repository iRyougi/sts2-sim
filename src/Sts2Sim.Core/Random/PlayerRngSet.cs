using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Random;

/// <summary>
/// Vendored verbatim from v0.109 game source (MegaCrit.Sts2.Core.Random.PlayerRngSet), with
/// deviations registered in Plan 02.5's deviation list:
///  - #21/#3: <see cref="CloneExact()"/> is new (not present in the game); needed for O(1) parallel
///    environment snapshots — continuation of the earlier deviation.
/// </summary>
public class PlayerRngSet
{
    private readonly Dictionary<PlayerRngType, Rng> _rngs = new();
    private readonly bool _usesSemanticKeys;
    private readonly KeyedRngTrace? _keyedTrace;
    private SemanticScope? _semanticScope;

    public bool UsesSemanticKeys => _usesSemanticKeys;

    public Rng Rewards => GetRng(PlayerRngType.Rewards);

    public Rng Shops => GetRng(PlayerRngType.Shops);

    public Rng Transformations => GetRng(PlayerRngType.Transformations);

    public ulong Seed { get; }

    /// <summary>偏离 #73：直接用 <c>seed</c>（run 种子）播种，不加玩家槽位偏移——本计划及可预见的
    /// 后续计划都只做单人，真实游戏的多人槽位偏移在此没有对应需求。</summary>
    public PlayerRngSet(ulong seed)
        : this(seed, usesSemanticKeys: false)
    {
    }

    private PlayerRngSet(ulong seed, bool usesSemanticKeys)
    {
        _usesSemanticKeys = usesSemanticKeys;
        _keyedTrace = usesSemanticKeys ? new KeyedRngTrace() : null;
        Seed = seed;
        PlayerRngType[] values = Enum.GetValues<PlayerRngType>();
        foreach (PlayerRngType playerRngType in values)
        {
            _rngs[playerRngType] = CreateRng(playerRngType);
        }
    }

    /// <summary>
    /// Creates deviation #329's label-generation-only semantic-key RNG set. The explicit method name
    /// prevents a Boolean flag from silently changing fidelity replay semantics.
    /// </summary>
    public static PlayerRngSet CreateKeyed(ulong seed) => new(seed, usesSemanticKeys: true);

    private PlayerRngSet(
        ulong seed,
        Dictionary<PlayerRngType, Rng> rngs,
        bool usesSemanticKeys,
        KeyedRngTrace? keyedTrace = null)
    {
        Seed = seed;
        _rngs = rngs;
        _usesSemanticKeys = usesSemanticKeys;
        _keyedTrace = keyedTrace;
    }

    /// <summary>
    /// 精确克隆全部流（含底层生成器状态）。模拟器快照主路径（偏离 #3/#21）。
    /// #329 keyed 副本保留根 seed/模式及独立 trace；相同 semantic key 仍产生相同值。
    /// </summary>
    public PlayerRngSet CloneExact()
    {
        var rngs = new Dictionary<PlayerRngType, Rng>();
        foreach (var (key, rng) in _rngs)
        {
            rngs[key] = rng.CloneExact();
        }
        return new PlayerRngSet(Seed, rngs, _usesSemanticKeys, _keyedTrace?.CloneExact());
    }

    /// <summary>
    /// 派生一份副本，其中 <paramref name="streamsToReseed"/> 列出的流用
    /// <paramref name="branchSeed"/> 重新播种（counter 归零），其余流精确克隆。
    /// <para>
    /// 偏离 #189：真实游戏没有这个操作。<paramref name="streamsToReseed"/> 必填且不得为空——
    /// 空列表在语义上等同于 <see cref="CloneExact"/>，允许它就等于允许"以为在采样、实际在复制"
    /// 这个不会报错的误用（见 Plan08b 设计简报 A.3）。
    /// </para>
    /// </summary>
    public PlayerRngSet CloneReseeded(
        ulong branchSeed,
        IReadOnlyCollection<PlayerRngType> streamsToReseed)
    {
        if (_usesSemanticKeys)
        {
            throw new InvalidOperationException(
                "A keyed RNG set cannot be reseeded by stream. Use CloneExact and semantic keys.");
        }

        ArgumentNullException.ThrowIfNull(streamsToReseed);
        if (streamsToReseed.Count == 0)
        {
            throw new ArgumentException(
                "CloneReseeded 至少要指定一条流；不重播种任何流请改用 CloneExact()。",
                nameof(streamsToReseed));
        }

        var rngs = new Dictionary<PlayerRngType, Rng>();
        foreach (PlayerRngType stream in streamsToReseed)
        {
            if (!_rngs.ContainsKey(stream))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(streamsToReseed), stream, "指定的 RNG 流不存在。");
            }
        }

        foreach (var (key, rng) in _rngs)
        {
            rngs[key] = streamsToReseed.Contains(key)
                ? rng.CloneReseeded(branchSeed, StringHelper.SnakeCase(key.ToString()))
                : rng.CloneExact();
        }
        return new PlayerRngSet(Seed, rngs, usesSemanticKeys: false);
    }

    private Rng CreateRng(PlayerRngType rngType)
    {
        string name = StringHelper.SnakeCase(rngType.ToString());
        return _usesSemanticKeys
            ? Rng.CreateSemanticKeyRequired(Seed + StringHelper.GetDeterministicHashCode(name))
            : new Rng(Seed, name);
    }

    /// <summary>
    /// Derives deviation #329's independent generator for one purpose. It is intentionally distinct
    /// from <see cref="CloneExact"/> and <see cref="CloneReseeded"/> rather than controlled by a Boolean.
    /// </summary>
    public Rng ForSemanticKey(PlayerRngType rngType, string semanticKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticKey);
        if (!_usesSemanticKeys)
        {
            return GetRng(rngType);
        }

        string streamName = StringHelper.SnakeCase(rngType.ToString());
        return Rng.CreateObserved(
            KeyedRngSeed.Derive(Seed, streamName, semanticKey),
            (ordinal, operation, result) =>
                _keyedTrace!.Record(streamName, semanticKey, ordinal, operation, result));
    }

    /// <summary>
    /// Uses an enclosing operation/slot scope when one exists; otherwise derives the supplied
    /// fallback purpose. Reward objects use this so indexed custom offers can provide their own
    /// slot while independently populated combat extras still have a stable purpose.
    /// </summary>
    internal Rng ForCurrentScopeOrSemanticKey(PlayerRngType rngType, string fallbackSemanticKey)
    {
        if (_usesSemanticKeys && _semanticScope is not null)
        {
            return _semanticScope.Get(rngType);
        }
        return ForSemanticKey(rngType, fallbackSemanticKey);
    }

    internal IReadOnlyList<KeyedRngDraw> GetKeyedDraws() =>
        _keyedTrace?.Snapshot() ?? Array.Empty<KeyedRngDraw>();

    public IDisposable BeginSemanticScope(string semanticKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticKey);
        if (!_usesSemanticKeys) return EmptyScope.Instance;
        var scope = new SemanticScope(this, _semanticScope, semanticKey);
        _semanticScope = scope;
        return scope;
    }

    public SerializablePlayerRngSet ToSerializable()
    {
        SerializablePlayerRngSet serializablePlayerRngSet = new SerializablePlayerRngSet
        {
            Seed = Seed,
        };
        foreach (var (key, rng2) in _rngs)
        {
            serializablePlayerRngSet.Rngs[key] = rng2.ToSerializable();
        }
        return serializablePlayerRngSet;
    }

    public static PlayerRngSet FromSerializable(SerializablePlayerRngSet save)
    {
        PlayerRngSet playerRngSet = new PlayerRngSet(save.Seed);
        foreach (var (key, serializable) in save.Rngs)
        {
            playerRngSet._rngs[key] = new Rng(serializable);
        }
        return playerRngSet;
    }

    public void LoadFromSerializable(SerializablePlayerRngSet save)
    {
        if (Seed != save.Seed)
        {
            throw new NotImplementedException("RngSet seed should not change during the run!");
        }
        foreach (var (key, serializable) in save.Rngs)
        {
            _rngs[key].LoadFromSerializable(serializable);
        }
    }

    public Rng GetRng(PlayerRngType rngType)
    {
        if (_usesSemanticKeys && _semanticScope is not null)
        {
            return _semanticScope.Get(rngType);
        }
        return _rngs[rngType];
    }

    private sealed class SemanticScope(
        PlayerRngSet owner,
        SemanticScope? parent,
        string semanticKey) : IDisposable
    {
        private readonly Dictionary<PlayerRngType, Rng> _derived = new();
        private bool _disposed;

        public Rng Get(PlayerRngType type)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_derived.TryGetValue(type, out Rng? rng))
            {
                rng = owner.ForSemanticKey(type, semanticKey);
                _derived.Add(type, rng);
            }
            return rng;
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (!ReferenceEquals(owner._semanticScope, this))
                throw new InvalidOperationException("Semantic RNG scopes must be disposed in LIFO order.");
            owner._semanticScope = parent;
            _disposed = true;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();
        public void Dispose() { }
    }
}
