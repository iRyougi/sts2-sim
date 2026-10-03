using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Content;

/// <summary>
/// act 内容定义。偏离 #46：不是 <c>AbstractModel</c> 子类——本计划没有任何 relic/power 需要监听
/// Act 相关 hook，也不需要 canonical/mutable 二态语义。
/// </summary>
public abstract class ActDefinition
{
    private EncounterBagState? _weakMonsterEncounterState;
    private EncounterBagState? _regularMonsterEncounterState;
    private EncounterBagState? _eliteEncounterState;
    private EncounterDefinition? _lastMonsterEncounter;
    internal ActDefinition CloneForVisibleHistory()
    {
        var clone = (ActDefinition)MemberwiseClone();
        clone._weakMonsterEncounterState = null;
        clone._regularMonsterEncounterState = null;
        clone._eliteEncounterState = null;
        clone._lastMonsterEncounter = null;
        clone._monsterEncountersPicked = 0;
        return clone;
    }

    private int _monsterEncountersPicked;

    internal void RebuildEncounterBagsFromHistory(
        IReadOnlyList<EncounterDefinition> visitedMonsterEncounters,
        IReadOnlyList<EncounterDefinition> visitedEliteEncounters)
    {
        _weakMonsterEncounterState = null;
        _regularMonsterEncounterState = null;
        _eliteEncounterState = null;
        _lastMonsterEncounter = null;
        _monsterEncountersPicked = 0;
        foreach (EncounterDefinition encounter in visitedMonsterEncounters)
        {
            bool weak = _monsterEncountersPicked < NumberOfWeakEncounters;
            EncounterDefinition[] pool = MonsterEncounters.Where(e => e.IsWeak == weak).ToArray();
            ref EncounterBagState? state = ref (weak
                ? ref _weakMonsterEncounterState : ref _regularMonsterEncounterState);
            ConsumeObserved(ref state, pool, encounter);
            _lastMonsterEncounter = encounter;
            _monsterEncountersPicked++;
        }
        foreach (EncounterDefinition encounter in visitedEliteEncounters)
            ConsumeObserved(ref _eliteEncounterState, EliteEncounters, encounter);
    }

    private static void ConsumeObserved(ref EncounterBagState? state,
        IReadOnlyList<EncounterDefinition> pool, EncounterDefinition observed)
    {
        state ??= new EncounterBagState();
        if (!state.Bag.Any())
            foreach (EncounterDefinition entry in pool) state.Bag.Add(entry, 1.0);
        if (!state.Bag.Remove(observed))
            throw new InvalidOperationException($"Observed encounter {observed.Name} is not in its native draw bag.");
        state.LastPicked = observed;
    }
    internal ActDefinition CloneForRun()
    {
        var clone = (ActDefinition)MemberwiseClone();
        clone._weakMonsterEncounterState = _weakMonsterEncounterState?.Clone();
        clone._regularMonsterEncounterState = _regularMonsterEncounterState?.Clone();
        clone._eliteEncounterState = _eliteEncounterState?.Clone();
        return clone;
    }

    /// <summary>幕序号，0 起。对应真实游戏 ActModel.Index。</summary>
    public abstract int Index { get; }

    public virtual bool IsDefault => true;

    /// <summary>Selects the acts with the standalone RNG used by StartRunLobby.BeginRunLocally.</summary>
    public static IReadOnlyList<ActDefinition> GetRandomList(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return GetRandomList(new Rng(StringHelper.GetDeterministicHashCode(seed), "act_selection"));
    }

    /// <summary>Selects one act per index in ModelDb.Acts order, including singleton RNG draws.</summary>
    public static IReadOnlyList<ActDefinition> GetRandomList(Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        // Deviation #321: all content is unlocked and discovered; omit UnderdocksEpoch/IsUnlocked
        // and the save-dependent preference for an undiscovered alternate act.
        IReadOnlyList<ActDefinition>[] actsByIndex =
        [
            [new Overgrowth(), new Underdocks()],
            [new Hive()],
            [new Glory()],
        ];
        return actsByIndex.Select(acts => rng.NextItem(acts)!).ToArray();
    }

    /// <summary>本幕的事件池。取代 RunState 里写死的 Act1EventPool。</summary>
    public abstract IReadOnlyList<Type> EventPool { get; }

    /// <summary>ActModel.AllEvents excludes shared events; concatenate before the single UpFront shuffle.</summary>
    public IReadOnlyList<Type> EffectiveEventPool => [.. EventPool, .. SharedEventPool.All];

    /// <summary>Ancient event candidates for this act; one is selected from the UpFront RNG at act start.</summary>
    /// <remarks>Deviation #196: unlock/epoch filters are not modeled. RunState allocates shared Ancients
    /// without replacement to later acts before room generation; this property contains act-specific Ancients.
    /// The simulator treats all implemented content as unlocked.</remarks>
    public abstract IReadOnlyList<Type> AncientPool { get; }

    public abstract int BaseNumberOfRooms { get; }

    public abstract int NumberOfWeakEncounters { get; }

    protected abstract IReadOnlyList<EncounterDefinition> MonsterEncounters { get; }

    protected abstract IReadOnlyList<EncounterDefinition> EliteEncounters { get; }

    /// <summary>偏离 #179：不移植依赖跨局 <c>UnlockState</c> 的 Act discovery-order 修改，
    /// 包括首次运行的普通/Elite 固定顺序和 Boss 发现顺序；本项目每局 episode 独立。
    /// Boss 候选因此直接随机抽取。</summary>
    protected abstract IReadOnlyList<EncounterDefinition> BossEncounters { get; }

    /// <summary>本幕公开的普通遭遇候选池，不是预抽顺序。</summary>
    public IReadOnlyList<EncounterDefinition> MonsterEncounterCandidates => MonsterEncounters;

    /// <summary>本幕公开的精英遭遇候选池，不是预抽顺序。</summary>
    public IReadOnlyList<EncounterDefinition> EliteEncounterCandidates => EliteEncounters;

    /// <summary>本幕公开的 Boss 遭遇候选池，不是预抽顺序。</summary>
    public IReadOnlyList<EncounterDefinition> BossEncounterCandidates => BossEncounters;

    /// <summary>Legacy extension seam retained for existing call sites and test-only act definitions.</summary>
    public virtual MapPointTypeCounts GetMapPointTypes(Rng mapRng) =>
        GetMapPointTypes(mapRng, new AscensionManager(AscensionLevel.None));

    public virtual MapPointTypeCounts GetMapPointTypes(Rng mapRng, AscensionManager ascension) =>
        GetMapPointTypes(mapRng);

    public EncounterDefinition PickEncounter(RoomType roomType, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return roomType switch
        {
            RoomType.Monster => PickMonsterEncounter(rng),
            RoomType.Elite => PickEliteEncounter(rng),
            RoomType.Boss => PickBossEncounter(rng),
            _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, null),
        };
    }

    /// <summary>抽一个 Boss 遭遇，但排除 <paramref name="excluded"/>。用于 A10 DoubleBoss 的第二个 Boss。
    /// 对照 <c>RunManager.GenerateRooms</c>：排除当前幕首领遭遇的其他首领遭遇。</summary>
    public EncounterDefinition PickEncounterExcluding(RoomType roomType, Rng rng, EncounterDefinition excluded)
    {
        if (roomType != RoomType.Boss)
        {
            throw new ArgumentOutOfRangeException(nameof(roomType), roomType, "Only boss encounters support exclusion.");
        }

        EncounterDefinition[] candidates = BossEncounters.Where(e => !e.Equals(excluded)).ToArray();
        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                $"Act {Index} has no second boss candidate distinct from the first.");
        }

        return rng.NextItem(candidates)!;
    }

    private EncounterDefinition PickMonsterEncounter(Rng rng)
    {
        bool useWeakPool = _monsterEncountersPicked < NumberOfWeakEncounters;
        EncounterDefinition[] encounters = MonsterEncounters
            .Where(encounter => encounter.IsWeak == useWeakPool)
            .ToArray();
        EnsurePoolNotEmpty(RoomType.Monster, encounters, useWeakPool ? "weak" : "regular");

        EncounterDefinition picked;
        if (useWeakPool)
        {
            picked = PickWithoutRepeating(
                ref _weakMonsterEncounterState,
                encounters,
                rng,
                _lastMonsterEncounter);
        }
        else
        {
            picked = PickWithoutRepeating(
                ref _regularMonsterEncounterState,
                encounters,
                rng,
                _lastMonsterEncounter);
        }

        _lastMonsterEncounter = picked;
        _monsterEncountersPicked++;
        return picked;
    }

    private EncounterDefinition PickEliteEncounter(Rng rng)
    {
        EnsurePoolNotEmpty(RoomType.Elite, EliteEncounters);
        return PickWithoutRepeating(ref _eliteEncounterState, EliteEncounters, rng);
    }

    private EncounterDefinition PickBossEncounter(Rng rng)
    {
        EnsurePoolNotEmpty(RoomType.Boss, BossEncounters);
        return rng.NextItem(BossEncounters)!;
    }

    private static EncounterDefinition PickWithoutRepeating(
        ref EncounterBagState? state,
        IReadOnlyList<EncounterDefinition> allEncounters,
        Rng rng,
        EncounterDefinition? previousOverride = null)
    {
        state ??= new EncounterBagState();
        if (!state.Bag.Any())
        {
            foreach (EncounterDefinition encounter in allEncounters)
            {
                state.Bag.Add(encounter, 1.0);
            }
        }

        EncounterDefinition? previous = previousOverride ?? state.LastPicked;
        EncounterDefinition? picked = state.Bag.GrabAndRemove(
            rng,
            candidate =>
                !ReferenceEquals(candidate, previous) &&
                !SharesTagWith(candidate, previous));
        picked ??= state.Bag.GrabAndRemove(rng);
        picked ??= allEncounters[0];
        state.LastPicked = picked;
        return picked;
    }

    private static void EnsurePoolNotEmpty(
        RoomType roomType,
        IReadOnlyCollection<EncounterDefinition> encounters,
        string? qualifier = null)
    {
        if (encounters.Count > 0)
        {
            return;
        }

        string poolName = qualifier is null ? roomType.ToString() : $"{qualifier} {roomType}";
        throw new InvalidOperationException($"Encounter pool for room type '{poolName}' cannot be empty.");
    }

    private static bool SharesTagWith(EncounterDefinition candidate, EncounterDefinition? previous) =>
        previous is not null &&
        candidate.Tags.Any(tag => tag != EncounterTag.None && previous.Tags.Contains(tag));

    private sealed class EncounterBagState
    {
        public GrabBag<EncounterDefinition> Bag { get; private set; } = new();

        public EncounterDefinition? LastPicked { get; set; }

        internal EncounterBagState Clone()
        {
            var clone = new EncounterBagState { LastPicked = LastPicked };
            clone.Bag = Bag.Clone();
            return clone;
        }
    }
}
