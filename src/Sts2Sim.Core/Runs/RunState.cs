using System.Collections.ObjectModel;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// Concrete run state. Map, room-stack, act, and odds members live on this concrete type so
/// Plan 03 combat code can keep depending only on the minimal <see cref="IRunState"/> surface.
///
/// 真实游戏的 RunManager.EnterAct 顺序是：淡出和清屏、退出当前房间，SetActInternal 切换当前幕；
/// SetActInternal 随即重置 ActFloor/幕内访问坐标和房间 ID，并将 UnknownMapPoint odds 复位到配置基值，
/// 然后加载幕资源并生成新地图，随后进入地图房。TotalFloor 不在此处累加，而是由跨幕地图历史的房间数求和；玩家、HP、牌组和遗物保持原对象，
/// 不会在换幕时回血。Ancient 是每幕地图上的节点，不是幕间转换的独立效果。
/// 本模拟器只在此类中重建幕定义相关的地图、事件和预抽遭遇；房间退出、存档、UI 和音乐由
/// RunEngine/基础设施负责或尚未建模（偏离建议 #193 起由 Task 4 统一登记）。
public sealed partial class RunState : IRunState
/// </summary>
{
    /// <summary>真实游戏 <c>ActModel.GenerateRooms</c> 里精英遭遇战槽位固定预抽 15 个，
    /// 与地图实际精英房间数量无关，是固定尝试 15 次的魔法数，并非按 act 参数化。</summary>
    private const int EliteEncounterPoolSize = 15;

    private readonly List<Player> _players = new();
    private readonly ActDefinition[] _acts;
    private readonly ReadOnlyCollection<ActDefinition> _readOnlyActs;
    private GeneratedActRooms[]? _generatedRooms;
    private readonly List<MapCoord> _visitedMapCoords = new();
    private readonly List<UnknownMapPointVisit> _visibleMapVisits = new();
    private int _unknownMapPointEntriesThisAct;
    public IReadOnlyList<UnknownMapPointVisit> VisibleUnknownMapPointVisits => _visibleMapVisits;
    private int _completedActFloors;
    private bool _selectingMapEventBeforeHistoryAppend;
    private bool _currentMapPointHasShop;
    internal bool PreviousMapPointHasShop { get; private set; }
    private readonly List<AbstractRoom> _currentRooms = new();
    private readonly List<RewardsSet> _activeRewardOffers = new();
    internal ForcedCombatOutcome? ForcedCombatResumeOutcome { get; set; }
    internal IReadOnlyList<RewardsSet> ActiveRewardOffers => _activeRewardOffers;
    internal void BeginActiveRewardOffer(RewardsSet offer) => _activeRewardOffers.Add(offer);
    internal void EndActiveRewardOffer() => _activeRewardOffers.RemoveAt(_activeRewardOffers.Count - 1);
    private List<Type> _eventSequence = null!;
    private readonly HashSet<ModelId> _visitedEventIds = new();
    // Actual Ancient entries are visible history, unlike pre-generated future rooms.
    private readonly Dictionary<int, Type> _visitedAncientTypes = new();
    private bool _hasVisibleAncientHistory = true;
    private List<EncounterDefinition> _normalEncounterSequence = null!;
    private List<EncounterDefinition> _eliteEncounterSequence = null!;
    private EncounterDefinition _bossEncounter = null!;
    private EncounterDefinition? _secondBossEncounter;
    /// <summary>本幕 Boss。玩家在进入本幕时即可在顶栏看到。</summary>
    public EncounterDefinition? CurrentActBossEncounter => _bossEncounter;

    /// <summary>A10 终幕的第二个 Boss；非双 Boss 幕为 null。</summary>
    public EncounterDefinition? CurrentActSecondBossEncounter => _secondBossEncounter;
    private int _bossEncountersVisited;
    private int _eventsVisited;
    private int _normalEncountersVisited;
    private int _eliteEncountersVisited;
    private int _nextRoomId;

    public ICardSelectionDecisionSource CardSelectionSource { get; private set; } =
        RunEngineCardSelectionDecisionSource.Instance;

    public RunRngSet Rng { get; }

    public RunOddsSet Odds { get; }

    public AscensionManager Ascension { get; }

    public IReadOnlyList<Player> Players => _players;

    public HeadlessProgress Progress { get; }
    public int WongoPointsEarned { get; internal set; }
    public bool FreedRepy { get; internal set; }
    private readonly List<ModelId> _completedQuests = [];
    public IReadOnlyList<ModelId> CompletedQuests => _completedQuests.AsReadOnly();
    internal void RecordQuestCompleted(ModelId id) => _completedQuests.Add(id);
    public HeadlessProgress ExportProgress() => new(checked(Progress.WongoPoints + WongoPointsEarned));

    public IReadOnlyList<ActDefinition> Acts => _readOnlyActs;

    public ActDefinition Act => _acts[CurrentActIndex];

    private Type _currentAncientEventType = null!;

    /// <summary>The Ancient event selected when rooms are first needed, after players are created.</summary>
    public Type CurrentAncientEventType
    {
        get
        {
            EnsureRoomsGenerated();
            return _currentAncientEventType;
        }
    }

    public RelicGrabBag? SharedRelicGrabBag { get; private set; }

    public ActMap Map { get; private set; } = null!;

    public int CurrentActIndex { get; private set; }

    public IReadOnlyList<MapCoord> VisitedMapCoords => _visitedMapCoords;

    /// <summary>跨幕累计的已访问房间数。对应真实游戏
    /// <c>RunState.TotalFloor</c>（<c>MapPointHistory</c> 逐幕求和）。
    /// <see cref="VisitedMapCoords"/> 在 <see cref="AdvanceToNextAct"/> 会被清空，
    /// 只表达本幕房间数，不能用于跨幕判定。仅在 <see cref="PullMapPointEvent"/>
    /// 同步筛选事件时，已登记的目的坐标尚未进入原生地图历史，故临时减一层；
    /// 筛选结束后立即恢复进入后的楼层。</summary>
    public int TotalFloor => _completedActFloors + _visitedMapCoords.Count
        - (_selectingMapEventBeforeHistoryAppend && _visitedMapCoords.Count > 0 ? 1 : 0);

    public MapCoord? CurrentMapCoord => _visitedMapCoords.Count > 0 ? _visitedMapCoords[^1] : null;

    public MapPoint? CurrentMapPoint => CurrentMapCoord.HasValue ? Map.GetPoint(CurrentMapCoord.Value) : null;

    public MapLocation MapLocation => new(CurrentMapCoord, CurrentActIndex);

    public int CurrentRoomCount => _currentRooms.Count;

    public AbstractRoom? CurrentRoom => _currentRooms.Count > 0 ? _currentRooms[^1] : null;

    public AbstractRoom? BaseRoom => _currentRooms.Count > 0 ? _currentRooms[0] : null;

    internal string SemanticLocationKey
    {
        get
        {
            MapCoord? coord = CurrentMapCoord;
            return $"act={CurrentActIndex}/floor={TotalFloor}" +
                   $"/map_col={coord?.col.ToString() ?? "none"}/map_row={coord?.row.ToString() ?? "none"}" +
                   $"/room_kind={CurrentRoom?.RoomType.ToString() ?? "none"}/room_id={CurrentRoom?.Id ?? 0}";
        }
    }

    // Event ordering is per-act in the game. RunState owns it only while this simulator has one Act 1.
    internal int EventsVisited => _eventsVisited;

    internal IReadOnlySet<ModelId> VisitedEventIds => _visitedEventIds;

    public bool IsGameOver => Players.Count > 0 && Players.All(p => p.Creature.IsDead);

    public RunState(string seed, int ascensionLevel = 0, HeadlessProgress? progress = null)
        : this(seed, ActDefinition.GetRandomList(seed), ascensionLevel, progress)
    {
    }

    public RunState(string seed, ActDefinition act, int ascensionLevel = 0, HeadlessProgress? progress = null)
        : this(seed, [act], ascensionLevel, progress)
    {
    }

    public RunState(string seed, IReadOnlyList<ActDefinition> acts, int ascensionLevel = 0, HeadlessProgress? progress = null)
        : this(new RunRngSet(seed), acts, ascensionLevel, progress)
    {
    }

    /// <summary>
    /// Creates deviation #329's non-fidelity RNG path used only by counterfactual label generation.
    /// The ordinary constructors remain the bit-identical sequential game path. This separate name,
    /// rather than a Boolean constructor parameter, keeps an accidental fidelity/keyed swap from
    /// compiling into a run that merely appears to gain no benefit from deeper search.
    /// </summary>
    public static RunState CreateKeyedForLabels(
        string seed,
        IReadOnlyList<ActDefinition> acts,
        int ascensionLevel = 0,
        HeadlessProgress? progress = null) =>
        new(RunRngSet.CreateKeyed(seed), acts, ascensionLevel, progress);

    private RunState(
        RunRngSet rng,
        IReadOnlyList<ActDefinition> acts,
        int ascensionLevel,
        HeadlessProgress? progress)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(acts);
        ActDefinition[] materializedActs = acts.ToArray();
        ValidateActs(materializedActs, nameof(acts));

        Rng = rng;
        Progress = progress ?? new HeadlessProgress();
        _acts = materializedActs;
        _readOnlyActs = Array.AsReadOnly(_acts);
        CurrentActIndex = 0;
        Ascension = new AscensionManager(ascensionLevel);
        GenerateCurrentMap();

        Odds = new RunOddsSet(Rng.UnknownMapPoint, new HookOddsAdapter(this));
    }

    /// <summary>
    /// Advances to the following configured act using rooms pre-generated at run creation.
    /// </summary>
    public void AdvanceToNextAct()
    {
        EnsureRoomsGenerated();
        if (CurrentActIndex >= _acts.Length - 1)
        {
            throw new InvalidOperationException("Cannot advance beyond the final act.");
        }

        CurrentActIndex++;
        _completedActFloors += _visitedMapCoords.Count;
        _visitedMapCoords.Clear();
        _currentMapPointHasShop = false;
        PreviousMapPointHasShop = false;
        _eventsVisited = 0;
        _normalEncountersVisited = 0;
        _eliteEncountersVisited = 0;
        _nextRoomId = 0;
        Odds.UnknownMapPoint.ResetToBase();
        _visibleMapVisits.Clear();
        _unknownMapPointEntriesThisAct = 0;
        InitializeCurrentAct();
        GenerateCurrentMap();
    }

    private static void ValidateActs(ActDefinition[] acts, string parameterName)
    {
        if (acts.Length == 0)
        {
            throw new ArgumentException("At least one act is required.", parameterName);
        }

        for (int i = 0; i < acts.Length; i++)
        {
            ActDefinition? act = acts[i];
            if (act is null)
            {
                throw new ArgumentException($"Act at index {i} cannot be null.", parameterName);
            }

            if (act.Index != i)
            {
                throw new ArgumentException($"Act at index {i} must have Index {i}.", parameterName);
            }
        }
    }
    /// <summary>Populate the real run/player pools before any room consumes UpFront.
    /// No-player room queries are diagnostic-only and commit that run to room generation;
    /// creating a player afterwards would invalidate already observed rooms and is rejected.</summary>
    internal RelicGrabBag CreatePlayerRelicGrabBag(Player player)
    {
        if (_generatedRooms is not null || TotalFloor != 0 || CurrentRoom is not null)
        {
            throw new InvalidOperationException("Players must be created before generating or entering run rooms.");
        }

        SharedRelicGrabBag ??= new RelicGrabBag(
            Rng.ForSemanticKey(RunRngType.UpFront, "run_setup/relic_bag/shared"),
            SharedRelicPool.Instance.AllRelics,
            includeAllRarities: true);
        return new RelicGrabBag(
            Rng.ForSemanticKey(
                RunRngType.UpFront,
                $"run_setup/relic_bag/character={player.Character.Id.Entry}"),
            SharedRelicPool.Instance.AllRelics.Concat(player.Character.RelicPool.AllRelics));
    }

    private void EnsureRoomsGenerated()
    {
        if (_generatedRooms is not null)
        {
            return;
        }

        _generatedRooms = GenerateAllActRooms();
        InitializeCurrentAct();
    }

    private GeneratedActRooms[] GenerateAllActRooms()
    {
        var remaining = new List<Type>(SharedAncientPool.All.Count);
        for (int index = 0; index < SharedAncientPool.All.Count; index++)
        {
            Type ancient = SharedAncientPool.All[index];
            if (!_visitedAncientTypes.ContainsValue(ancient)) remaining.Add(ancient);
        }
        remaining.UnstableShuffle(
            Rng.ForSemanticKey(RunRngType.UpFront, "run_setup/shared_ancients/shuffle"));
        var subsets = new IReadOnlyList<Type>[_acts.Length];
        subsets[0] = Array.Empty<Type>();
        // RunManager.GenerateRooms: allocate ALL shared subsets before ANY room generation.
        for (int i = 1; i < _acts.Length; i++)
        {
            int count = Rng.ForSemanticKey(
                RunRngType.UpFront,
                $"run_setup/shared_ancients/act={i}/count").NextInt(remaining.Count + 1);
            List<Type> subset = remaining.Take(count).ToList();
            remaining = remaining.Except(subset).ToList();
            subsets[i] = subset;
        }
        var generated = new GeneratedActRooms[_acts.Length];
        for (int i = 0; i < _acts.Length; i++)
            generated[i] = GenerateActRooms(_acts[i], i, subsets[i]);
        return generated;
    }

    private GeneratedActRooms GenerateActRooms(ActDefinition act, int actIndex, IReadOnlyList<Type> sharedAncients)
    {
        List<Type> events = act.EffectiveEventPool.ToList();
        events.UnstableShuffle(Rng.ForSemanticKey(
            RunRngType.UpFront,
            $"run_setup/act={actIndex}/events/shuffle"));

        // 真实游戏 RunManager.SetUpNewSingleplayer -> InitializeNewRun -> GenerateRooms() 在创建 run
        // 的那一刻（玩家还未看到 Neow）就用 State.Rng.UpFront 把整幕的普通/精英/Boss/先古之民
        // 遭遇战一次性抽完，按顺序存起来，房间真正被访问时只是按序取用，不再消耗随机数。
        // 偏离 #46/#179 之前遗留的实现（已修复）曾经用错了 RNG 流（CombatCardGeneration）且延迟到
        // 房间实际被访问时才抽——两个问题叠加导致抽到的遭遇战和真实游戏对不上。
        var normalEncounters = new List<EncounterDefinition>(act.BaseNumberOfRooms);
        for (int i = 0; i < act.BaseNumberOfRooms; i++)
        {
            normalEncounters.Add(act.PickEncounter(
                RoomType.Monster,
                Rng.ForSemanticKey(
                    RunRngType.UpFront,
                    $"run_setup/act={actIndex}/encounter/normal/slot={i}")));
        }

        var eliteEncounters = new List<EncounterDefinition>(EliteEncounterPoolSize);
        for (int i = 0; i < EliteEncounterPoolSize; i++)
        {
            eliteEncounters.Add(act.PickEncounter(
                RoomType.Elite,
                Rng.ForSemanticKey(
                    RunRngType.UpFront,
                    $"run_setup/act={actIndex}/encounter/elite/slot={i}")));
        }

        EncounterDefinition boss = act.PickEncounter(
            RoomType.Boss,
            Rng.ForSemanticKey(
                RunRngType.UpFront,
                $"run_setup/act={actIndex}/encounter/boss/slot=0"));

        // The game immediately selects and stores an Ancient from the act candidates on UpFront.
        // NextItem consumes UpFront even for one candidate; drawing again on room entry would shift the stream.
        Type ancient = _visitedAncientTypes.TryGetValue(actIndex, out Type? visitedAncient)
            ? visitedAncient
            : Rng.ForSemanticKey(
                RunRngType.UpFront,
                $"run_setup/act={actIndex}/ancient/slot=0")
                .NextItem(act.AncientPool.Concat(sharedAncients))
                ?? throw new InvalidOperationException($"Ancient pool for act {actIndex} cannot be empty.");

        // A10 DoubleBoss 只作用于最终幕，对照 RunManager.GenerateRooms。
        // ActModel.GenerateRooms 先抽 Boss、再抽 Ancient；返回后才从排除首个 Boss 的候选里抽第二个。
        // 抽取只在 DoubleBoss 生效时发生，A0 的 UpFront 流不受影响。
        EncounterDefinition? secondBoss = null;
        if (Ascension.HasLevel(AscensionLevel.DoubleBoss) && actIndex == _acts.Length - 1)
        {
            secondBoss = act.PickEncounterExcluding(
                RoomType.Boss,
                Rng.ForSemanticKey(
                    RunRngType.UpFront,
                    $"run_setup/act={actIndex}/encounter/boss/slot=1"),
                boss);
        }
        return new GeneratedActRooms(events, normalEncounters, eliteEncounters, boss, secondBoss, ancient);
    }

    private void InitializeCurrentAct()
    {
        GeneratedActRooms rooms = _generatedRooms![CurrentActIndex];
        _eventSequence = rooms.Events;
        _normalEncounterSequence = rooms.NormalEncounters;
        _eliteEncounterSequence = rooms.EliteEncounters;
        _bossEncounter = rooms.Boss;
        _secondBossEncounter = rooms.SecondBoss;
        _currentAncientEventType = rooms.Ancient;
        _bossEncountersVisited = 0;
    }

    private void GenerateCurrentMap()
    {
        ActDefinition act = Act;
        var mapRng = new Rng(Rng.Seed, $"act_{CurrentActIndex + 1}_map");
        MapPointTypeCounts pointTypeCounts = act.GetMapPointTypes(mapRng, Ascension);
        Map = new StandardActMap(
            mapRng,
            act.BaseNumberOfRooms,
            pointTypeCounts,
            hasSecondBoss: Ascension.HasLevel(AscensionLevel.DoubleBoss) && CurrentActIndex == _acts.Length - 1);
        Map = Hooks.Hook.ModifyGeneratedMap(this, Map, CurrentActIndex);
        // Run setup is synchronous; map-generated listeners contain no player-choice suspension.
        Hooks.Hook.AfterMapGenerated(this, Map, CurrentActIndex).GetAwaiter().GetResult();
    }

    /// <summary>Regenerate the current act map when an obtained relic replaces its topology.</summary>
    internal void RegenerateCurrentMap() => GenerateCurrentMap();

    /// <summary>按真实游戏 <c>RoomSet.NextNormalEncounter</c>/<c>NextEliteEncounter</c>/<c>NextBossEncounter</c>
    /// 的取用语义：从开局一次性抽好的序列里按访问次数取，取用即推进指针。</summary>
    public EncounterDefinition PullNextEncounter(RoomType roomType)
    {
        EnsureRoomsGenerated();
        return roomType switch
        {
            RoomType.Monster => _normalEncounterSequence[_normalEncountersVisited++ % _normalEncounterSequence.Count],
            RoomType.Elite => _eliteEncounterSequence[_eliteEncountersVisited++ % _eliteEncounterSequence.Count],
            RoomType.Boss => PullBossEncounter(),
            _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, null),
        };
    }

    /// <summary>逐字对照 <c>RoomSet.NextBossEncounter</c>：第一次取 Boss，之后若有第二 Boss 则取它。</summary>
    private EncounterDefinition PullBossEncounter()
    {
        EncounterDefinition chosen = _bossEncountersVisited != 0 && _secondBossEncounter is not null
            ? _secondBossEncounter
            : _bossEncounter;
        _bossEncountersVisited++;
        return chosen;
    }

    internal void ConfigureCardSelectionSource(ICardSelectionDecisionSource source) =>
        CardSelectionSource = source ?? throw new ArgumentNullException(nameof(source));

    public void AddPlayer(Player player) => _players.Add(player);

    internal UnknownMapPointPublicRules CaptureVisibleUnknownMapPointRules(MapPoint point)
    {
        bool hasJuzu = false;
        bool goldenPath = false;
        bool lanternKey = false;
        foreach (AbstractModel listener in IterateHookListeners(null))
        {
            if (listener is Models.Relics.JuzuBracelet) hasJuzu = true;
            if (listener is Models.Relics.GoldenCompass compass && compass.GoldenPathAct == CurrentActIndex)
                goldenPath = true;
            if (listener is Models.Cards.LanternKey && CurrentActIndex == 2) lanternKey = true;
        }
        // Capturing public increases invokes ModifyOddsIncreaseForUnrolledRoomType once
        // before the actual roll. No current listener overrides it with stateful effects;
        // a future stateful override requires a side-effect-free public-rule capture path.
        return new(
            Odds.UnknownMapPoint.PublicBaseRules,
            Odds.UnknownMapPoint.CapturePublicUnrolledIncreases(),
            PreviousMapPointHasShop,
            point.Children.Count > 0 && point.Children.All(child => child.PointType == MapPointType.Shop),
            hasJuzu, goldenPath, lanternKey);
    }

    internal void RecordVisibleUnknownMapPointEntry(RoomType actualType, UnknownMapPointPublicRules rules)
    {
        _unknownMapPointEntriesThisAct++;
        _visibleMapVisits.Add(new(actualType, rules));
    }

    private void RequireVisibleUnknownMapPointHistory()
    {
        if (_unknownMapPointEntriesThisAct != _visibleMapVisits.Count)
            throw new MissingVisibleUnknownHistoryException();
    }

    public bool AddVisitedMapCoord(MapCoord coord)
    {
        if (_visitedMapCoords.Contains(coord))
        {
            return false;
        }

        PreviousMapPointHasShop = _currentMapPointHasShop;
        _currentMapPointHasShop = false;
        _visitedMapCoords.Add(coord);
        return true;
    }

    // The upstream previous map-point history includes nested rooms, not just the map node's type.
    internal void RecordShopRoomEntry()
    {
        if (CurrentMapCoord.HasValue)
            _currentMapPointHasShop = true;
    }

    internal void RecordAncientRoomVisit(Type ancientType)
    {
        EnsureRoomsGenerated();
        _visitedAncientTypes[CurrentActIndex] = ancientType;
        _eventsVisited++;
    }

    // Native RunManager creates the event before appending this node to MapPointHistory.
    // Our visited coordinates already include the destination; expose the native history
    // floor while selecting, then restore the entered floor before event initialization.
    internal Type PullMapPointEvent()
    {
        _selectingMapEventBeforeHistoryAppend = true;
        try { return PullNextEvent(); }
        finally { _selectingMapEventBeforeHistoryAppend = false; }
    }

    public Type PullNextEvent()
    {
        EnsureRoomsGenerated();
        if (_eventSequence.Count == 0)
        {
            throw new InvalidOperationException("The current act has no events.");
        }

        for (int index = 0; index < _eventSequence.Count; index++)
        {
            Type candidateType = _eventSequence[_eventsVisited % _eventSequence.Count];
            var candidate = (EventModel)ModelDb.Get(candidateType).MutableClone();
            if (candidate.CanAppearNaturally && !_visitedEventIds.Contains(candidate.Id) && candidate.IsAllowed(this))
            {
                return AcceptEvent(candidateType, candidate.Id);
            }

            _eventsVisited++;
        }

        for (int index = 0; index < _eventSequence.Count; index++, _eventsVisited++)
        {
            Type fallbackType = _eventSequence[_eventsVisited % _eventSequence.Count];
            var fallback = (EventModel)ModelDb.Get(fallbackType);
            if (!fallback.CanAppearNaturally) continue;
            System.Diagnostics.Trace.TraceWarning(
                "All unique events were rejected; allowing visited or temporarily disallowed event {0}.", fallback.Id);
            return AcceptEvent(fallbackType, fallback.Id);
        }
        throw new InvalidOperationException("The current act has no naturally selectable events.");
    }

    private Type AcceptEvent(Type eventType, ModelId eventId)
    {
        EventModel modified = Hooks.Hook.ModifyNextEvent(this, (EventModel)ModelDb.Get(eventType));
        eventType = modified.GetType();
        eventId = modified.Id;
        _visitedEventIds.Add(eventId);

        // Ordinary event selection owns its successful advancement; Ancient rooms advance
        // separately on their first top-level entry because they do not pull from this list.
        _eventsVisited++;
        return eventType;
    }

    public AbstractRoom PopCurrentRoom()
    {
        if (_currentRooms.Count == 0)
        {
            throw new InvalidOperationException("Not in any rooms.");
        }

        AbstractRoom last = _currentRooms[^1];
        _currentRooms.RemoveAt(_currentRooms.Count - 1);
        return last;
    }

    public void PushRoom(AbstractRoom room)
    {
        if (_currentRooms.Contains(room))
        {
            throw new InvalidOperationException("Already in this room.");
        }

        _currentRooms.Add(room);
    }

    public int GetAndIncrementNextRoomId()
    {
        int id = _nextRoomId;
        _nextRoomId++;
        return id;
    }

    /// <summary>偏离 #69：现在会枚举玩家的 <see cref="Player.Relics"/>，但仍不链上真实游戏还有的
    /// <c>Ancients</c>/<c>Modifiers</c>/<c>BadgeModels</c>（均未移植）；本条也修复了此前"非战斗期
    /// hook 恒无人监听"的缺口。</summary>
    public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState)
    {
        foreach (Player player in _players)
        {
            // Hooks may obtain a relic (for example a merchant relic purchase), so
            // enumerate a stable listener snapshot for this dispatch.
            foreach (RelicModel relic in player.Relics.ToArray())
            {
                if (!relic.IsMelted)
                    yield return relic;
            }

            foreach (PotionModel potion in player.PotionSlots.OfType<PotionModel>())
            {
                yield return potion;
            }

            if (childCombatState is null)
            {
                foreach (CardModel card in player.Deck.Cards.ToArray())
                {
                    yield return card;
                }
            }
        }

        if (childCombatState != null)
        {
            foreach (AbstractModel listener in childCombatState.IterateHookListeners())
            {
                yield return listener;
            }
        }
    }
}
