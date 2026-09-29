using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// Vendored verbatim from v0.109 game source (MegaCrit.Sts2.Core.Runs.RunRngSet), with
/// deviations registered in Plan 02.5's deviation list:
///  - #24: <c>_mockInstance</c>/<c>GetMockInstance</c>/<c>TestMode</c> (multiplayer/global test
///    switch, out of scope) are not ported — continuation of Plan 01 deviation #4.
///  - #21/#3: <see cref="CloneExact()"/> is new (not present in the game); needed for O(1) parallel
///    environment snapshots — continuation of the earlier deviation.
///  - #329: semantic-key derivation is a label-generation-only RNG mode absent from the game.
///    The ordinary constructor remains the sequential fidelity path.
/// </summary>
public class RunRngSet
{
    private readonly Dictionary<RunRngType, Rng> _rngs = new();
    private readonly bool _usesSemanticKeys;
    private readonly KeyedRngTrace? _keyedTrace;
    private SemanticScope? _semanticScope;

    public bool UsesSemanticKeys => _usesSemanticKeys;

    /// <summary>
    /// We generate a string for the seed that gets hashed to a ulong for the actual thing
    /// passed to the RNGs. This is the original string that was input, for display purposes.
    /// </summary>
    public string StringSeed { get; }

    /// <summary>
    /// The seed that was hashed from the InputSeed.
    /// </summary>
    public ulong Seed { get; }

    public Rng UpFront => GetRng(RunRngType.UpFront);

    public Rng Shuffle => GetRng(RunRngType.Shuffle);

    public Rng UnknownMapPoint => GetRng(RunRngType.UnknownMapPoint);

    public Rng CombatCardGeneration => GetRng(RunRngType.CombatCardGeneration);

    public Rng CombatPotionGeneration => GetRng(RunRngType.CombatPotionGeneration);

    public Rng CombatCardSelection => GetRng(RunRngType.CombatCardSelection);

    public Rng CombatEnergyCosts => GetRng(RunRngType.CombatEnergyCosts);

    public Rng CombatTargets => GetRng(RunRngType.CombatTargets);

    public Rng MonsterAi => GetRng(RunRngType.MonsterAi);

    public Rng Niche => GetRng(RunRngType.Niche);

    public Rng CombatOrbGeneration => GetRng(RunRngType.CombatOrbs);

    public Rng TreasureRoomRelics => GetRng(RunRngType.TreasureRoomRelics);

    public RunRngSet(string seed)
        : this(seed, usesSemanticKeys: false)
    {
    }

    private RunRngSet(string seed, bool usesSemanticKeys)
    {
        _usesSemanticKeys = usesSemanticKeys;
        _keyedTrace = usesSemanticKeys ? new KeyedRngTrace() : null;
        StringSeed = seed;
        if (seed.StartsWith("old"))
        {
            string stringSeed = StringSeed;
            int length = "old".Length;
            Seed = (uint)StringHelper.GetDeterministicHashCodeOld(stringSeed.Substring(length, stringSeed.Length - length));
        }
        else
        {
            Seed = StringHelper.GetDeterministicHashCode(seed);
        }
        RunRngType[] values = Enum.GetValues<RunRngType>();
        foreach (RunRngType runRngType in values)
        {
            _rngs[runRngType] = CreateRng(runRngType);
        }
    }

    /// <summary>
    /// Creates deviation #329's semantic-key RNG set for counterfactual labels. This is deliberately
    /// named separately from the sequential constructor: a Boolean switch could silently put fidelity
    /// replay on the wrong semantics, an error that looks only like deeper search without better results.
    /// </summary>
    public static RunRngSet CreateKeyed(string seed) => new(seed, usesSemanticKeys: true);

    private RunRngSet(
        string stringSeed,
        ulong seed,
        Dictionary<RunRngType, Rng> rngs,
        bool usesSemanticKeys,
        KeyedRngTrace? keyedTrace = null)
    {
        StringSeed = stringSeed;
        Seed = seed;
        _rngs = rngs;
        _usesSemanticKeys = usesSemanticKeys;
        _keyedTrace = keyedTrace;
    }

    /// <summary>
    /// 精确克隆全部流（含底层生成器状态）。模拟器快照主路径（偏离 #3/#21）。
    /// 在 #329 keyed 模式下保留根 seed 和 keyed 模式：相同 semantic key 仍派生相同值，
    /// 但 trace 与后续局部抽取彼此独立。这是战斗搜索快照所需语义，不会回退到顺序流。
    /// </summary>
    public RunRngSet CloneExact()
    {
        var rngs = new Dictionary<RunRngType, Rng>();
        foreach (var (key, rng) in _rngs)
        {
            rngs[key] = rng.CloneExact();
        }
        return new RunRngSet(
            StringSeed,
            Seed,
            rngs,
            _usesSemanticKeys,
            _keyedTrace?.CloneExact());
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
    public RunRngSet CloneReseeded(
        ulong branchSeed,
        IReadOnlyCollection<RunRngType> streamsToReseed)
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

        var rngs = new Dictionary<RunRngType, Rng>();
        foreach (RunRngType stream in streamsToReseed)
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
        return new RunRngSet(StringSeed, Seed, rngs, usesSemanticKeys: false);
    }

    private Rng CreateRng(RunRngType rngType)
    {
        string name = StringHelper.SnakeCase(rngType.ToString());
        return _usesSemanticKeys
            ? Rng.CreateSemanticKeyRequired(Seed + StringHelper.GetDeterministicHashCode(name))
            : new Rng(Seed, name);
    }

    /// <summary>
    /// Derives deviation #329's independent generator for one purpose. This explicit name is mutually
    /// exclusive with <see cref="CloneExact"/> and <see cref="CloneReseeded"/> semantics; it is not a
    /// Boolean option because using the wrong operation otherwise succeeds silently.
    /// </summary>
    public Rng ForSemanticKey(RunRngType rngType, string semanticKey)
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

    internal IReadOnlyList<KeyedRngDraw> GetKeyedDraws() =>
        _keyedTrace?.Snapshot() ?? Array.Empty<KeyedRngDraw>();

    /// <summary>
    /// Opens a semantic operation boundary for legacy call sites that obtain a named stream
    /// through its property. Each stream is independently derived from <paramref name="semanticKey"/>
    /// and cached for the lifetime of the scope, so multiple draws within one purpose retain their
    /// local order without advancing any shared run stream.
    /// </summary>
    public IDisposable BeginSemanticScope(string semanticKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticKey);
        if (!_usesSemanticKeys)
        {
            return EmptyScope.Instance;
        }

        var scope = new SemanticScope(this, _semanticScope, semanticKey);
        _semanticScope = scope;
        return scope;
    }

    public SerializableRunRngSet ToSerializable()
    {
        SerializableRunRngSet serializableRunRngSet = new SerializableRunRngSet
        {
            Seed = StringSeed,
        };
        foreach (var (key, rng2) in _rngs)
        {
            serializableRunRngSet.Rngs[key] = rng2.ToSerializable();
        }
        return serializableRunRngSet;
    }

    public static RunRngSet FromSave(SerializableRunRngSet save)
    {
        RunRngSet runRngSet = new RunRngSet(save.Seed);
        foreach (var (key, serializable) in save.Rngs)
        {
            runRngSet._rngs[key] = new Rng(serializable);
        }
        return runRngSet;
    }

    public void LoadFromSerializable(SerializableRunRngSet save)
    {
        if (StringSeed != save.Seed)
        {
            throw new NotImplementedException("RngSet seed should not change during the run!");
        }
        foreach (var (key, serializable) in save.Rngs)
        {
            _rngs[key].LoadFromSerializable(serializable);
        }
    }

    /// <summary>
    /// ONLY USE THIS FOR TESTING!
    /// Mock out the specified RNG type to use the specified seed.
    /// </summary>
    public void MockRng(RunRngType rngType, ulong seed)
    {
        _rngs[rngType] = new Rng(seed);
    }

    public Rng GetRng(RunRngType rngType)
    {
        if (_usesSemanticKeys && _semanticScope is not null)
        {
            return _semanticScope.Get(rngType);
        }
        return _rngs[rngType];
    }

    private sealed class SemanticScope(
        RunRngSet owner,
        SemanticScope? parent,
        string semanticKey) : IDisposable
    {
        private readonly Dictionary<RunRngType, Rng> _derived = new();
        private bool _disposed;

        public Rng Get(RunRngType type)
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
            {
                throw new InvalidOperationException("Semantic RNG scopes must be disposed in LIFO order.");
            }
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
