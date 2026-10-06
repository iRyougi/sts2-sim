using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rewards;
using System.Runtime.CompilerServices;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// Run loop engine. Holds a <see cref="RunState"/> and drives map choice, room resolution,
/// room entry, room completion, visit marking, and looping until the boss is defeated or the
/// players are dead.
/// </summary>
public sealed class RunEngine
{
    public sealed record Result(bool Won, bool ReachedBoss, int FloorsVisited, int FinalPlayerHp)
    {
        /// <summary>结束时玩家仍然存活。与 <see cref="Won"/> 无关：被 <c>maxFloors</c>
        /// 截断的局没有通关，但玩家是活着的。</summary>
        public bool Survived { get; init; } = true;

        /// <summary>本局因 <c>maxFloors</c> 上限提前收束，而非死亡或通关。</summary>
        public bool Truncated { get; init; }

        /// <summary>已打通的幕数（打通该幕 Boss 即计一幕）。</summary>
        public int ActsCleared { get; init; }

        /// <summary>终止原因。<c>Won</c>/<c>Survived</c>/<c>Truncated</c> 三个布尔推不出
        /// "地图耗尽"与"层数上限"的区别，这两者一个是缺陷一个是正常收束。</summary>
        public RunOutcome Outcome { get; init; } = RunOutcome.FloorLimitReached;

        /// <summary>仅在 <see cref="Outcome"/> 为 <see cref="RunOutcome.MapExhausted"/> 时有意义：
        /// 判定"走到头"的那一刻，当前地图点其实还连着子节点。
        ///
        /// **为真即引擎缺陷**——有路却取不到，正是偏离 #320 的形态。
        /// 探针可以据此自动判定，不必再人工去数路径条数。</summary>
        public bool MapExhaustedWithReachableChildren { get; init; }
    }

    private const int MaximumEventChoices = 100;

    private readonly RunState _runState;
    private readonly Func<IReadOnlyList<MapPoint>, MapPoint> _choosePoint;
    private readonly Func<RunState, AbstractRoom> _createAncientEventRoom;
    private readonly Func<RunState, AbstractRoom> _createEventRoom;
    private readonly IRunRecorder? _recorder;
    private readonly bool _useAvailablePotions;

    /// <summary>每场战斗的玩家回合上限，<c>null</c> 表示不限（生产路径的默认）。
    ///
    /// <see cref="DriveCombatToCompletion"/> 本身是 <c>while (engine.IsInProgress)</c>，
    /// 没有出口。正常跑局里玩家会死，所以战斗一定结束；但把玩家设成打不死之后
    /// （不变量探针就是这么做的），一场杀不掉怪的战斗会永远转下去。
    /// 探针传一个上限，超限抛 <see cref="CombatTurnLimitExceededException"/>，
    /// 把"这场战斗打不完"变成可观测的结果而不是挂起。</summary>
    private readonly int? _combatTurnLimit;
    private readonly Func<EventModel, Task<CustomEventDecision>> _chooseCustomEventAction;

    [OverloadResolutionPriority(1)]
    public RunEngine(
        RunState runState,
        Func<IReadOnlyList<MapPoint>, MapPoint> choosePoint,
        Func<RunState, AbstractRoom>? createAncientEventRoom = null)
        : this(
            runState,
            choosePoint,
            createAncientEventRoom,
            recorder: null,
            useAvailablePotions: false,
            createEventRoom: null)
    {
    }

    public RunEngine(
        RunState runState,
        Func<IReadOnlyList<MapPoint>, MapPoint> choosePoint,
        Func<RunState, AbstractRoom>? createAncientEventRoom = null,
        IRunRecorder? recorder = null,
        bool useAvailablePotions = false)
        : this(
            runState,
            choosePoint,
            createAncientEventRoom,
            recorder,
            useAvailablePotions,
            createEventRoom: null)
    {
    }

    public RunEngine(
        RunState runState,
        Func<IReadOnlyList<MapPoint>, MapPoint> choosePoint,
        Func<RunState, AbstractRoom>? createAncientEventRoom,
        IRunRecorder? recorder,
        bool useAvailablePotions,
        Func<RunState, AbstractRoom>? createEventRoom,
        Func<EventModel, Task<CustomEventDecision>>? chooseCustomEventAction = null,
        int? combatTurnLimit = null)
    {
        _combatTurnLimit = combatTurnLimit;
        _runState = runState;
        _choosePoint = choosePoint;
        _createAncientEventRoom = createAncientEventRoom ?? RoomFactory.CreateAncientEventRoom;
        _createEventRoom = createEventRoom ?? (state => RoomFactory.CreateRoom(state, RoomType.Event));
        _recorder = recorder;
        _useAvailablePotions = useAvailablePotions;
        _chooseCustomEventAction = chooseCustomEventAction ??
            (@event => Task.FromResult(CustomEventDecisionPolicy.ChooseDefault(@event)));
    }

    public event Action<MapPoint, RoomType>? OnRoomResolved;

    public async Task<Result> RunAsync(int maxFloors)
    {
        _recorder?.BeginRun(_runState);

        int recorderFloorsVisited = 0;
        if (_runState.IsGameOver)
        {
            return CompleteRun(won: false, reachedBoss: false, floorsVisited: 0, recorderFloorsVisited,
                outcome: RunOutcome.AlreadyOver);
        }

        MapPoint startingPoint = _runState.Map.StartingMapPoint;
        _runState.AddVisitedMapCoord(startingPoint.coord);
        if (startingPoint.PointType == MapPointType.Ancient)
        {
            _recorder?.EnterFloor(startingPoint, RoomType.Event);
            bool ancientProcessed = false;
            try
            {
                await DriveAncientStartingRoomAsync();
                ancientProcessed = true;
            }
            finally
            {
                if (ancientProcessed)
                {
                    _recorder?.ExitFloor();
                }
            }

            recorderFloorsVisited++;
        }
        else
        {
            await DriveAncientStartingRoomAsync();
        }

        if (_runState.IsGameOver)
        {
            return CompleteRun(won: false, reachedBoss: false, floorsVisited: 0, recorderFloorsVisited,
                outcome: RunOutcome.AlreadyOver);
        }

        int floorsVisited = 0;
        int actsCleared = 0;
        bool exhaustedMap = false;
        bool exhaustedWithReachableChildren = false;
        MapPoint current = _runState.Map.StartingMapPoint;

        while (floorsVisited < maxFloors)
        {
            if (!ReferenceEquals(_runState.Map.GetPoint(current.coord), current))
                throw new InvalidOperationException($"Current map point {current.coord} does not belong to the current map; the map was replaced before travel.");

            IReadOnlyList<MapPoint> travelable = MapTravel.GetTravelablePointsFrom(_runState, current).ToList();
            if (travelable.Count == 0)
            {
                exhaustedMap = true;
                // 取点入口给了空集合，但当前点可能其实还连着子节点——那是取点逻辑的缺陷
                // 而不是真的走到头（偏离 #320）。在这里留证据，供探针机器判定。
                exhaustedWithReachableChildren = current.Children.Count > 0;
                break;
            }

            MapPoint chosen = _choosePoint(travelable);
            _runState.AddVisitedMapCoord(chosen.coord);
            floorsVisited++;

            RoomType roomType = RoomFactory.ResolveRoomType(_runState, chosen);
            _recorder?.EnterFloor(chosen, roomType);
            NonCombatPlayerSnapshot? eventBefore = _recorder is not null && roomType == RoomType.Event
                ? NonCombatPlayerSnapshot.Capture(_runState.Players[0])
                : null;
            AbstractRoom room = roomType == RoomType.Event
                ? _createEventRoom(_runState)
                : RoomFactory.CreateRoom(_runState, roomType);
            CombatRecordingObserver? combatObserver = room is CombatRoom combatToObserve
                ? ConfigureCombatObserver(combatToObserve)
                : null;
            _runState.PushRoom(room);
            try { await room.Enter(_runState); }
            catch
            {
                if (room is EventRoom failedEventRoom)
                {
                    await failedEventRoom.Exit(_runState);
                    _runState.PopCurrentRoom();
                }
                throw;
            }
            FloorDetail? floorDetail = null;
            if (room is CombatRoom combatRoom)
            {
                await DriveCombatToCompletion(combatRoom, combatObserver: combatObserver);
            }
            else if (room is EventRoom eventRoom)
            {
                try
                {
                    floorDetail = await DriveEventAsync(eventRoom, eventBefore);
                }
                catch
                {
                    try
                    {
                        await room.Exit(_runState);
                    }
                    catch
                    {
                        // Preserve the original event/forced-combat failure.
                    }

                    try
                    {
                        if (ReferenceEquals(_runState.CurrentRoom, room))
                        {
                            _runState.PopCurrentRoom();
                        }
                    }
                    catch
                    {
                        // Preserve the original event/forced-combat failure.
                    }

                    throw;
                }
            }
            else if (room is RestSiteRoom restSiteRoom)
            {
                floorDetail = await DriveRestSiteToCompletion(restSiteRoom);
            }
            else if (room is MerchantRoom merchantRoom)
            {
                while (merchantRoom.TryDequeuePendingRewardOffer(out RewardsSet? rewards)) await ResolveRewardsAsync(rewards);
                if (_recorder is not null) floorDetail = new ShopFloorDetail(Array.Empty<PurchaseRecord>());
            }
            else if (room is TreasureRoom treasureRoom)
            {
                floorDetail = CreateTreasureFloorDetail(treasureRoom);
            }

            if (floorDetail is not null)
            {
                _recorder!.RecordFloorDetail(floorDetail);
            }
            await room.Exit(_runState);
            _runState.PopCurrentRoom();
            _recorder?.ExitFloor();
            recorderFloorsVisited++;

            OnRoomResolved?.Invoke(chosen, roomType);

            if (_runState.IsGameOver)
            {
                return CompleteRun(won: false, reachedBoss: chosen.PointType == MapPointType.Boss, floorsVisited, recorderFloorsVisited,
                    actsCleared: actsCleared, outcome: RunOutcome.PlayerDefeated);
            }

            if (chosen.PointType == MapPointType.Boss)
            {
                // A10 DoubleBoss：第一个 Boss 之后还有第二个，此时既不通关也不推进幕。
                if (_runState.Map.SecondBossMapPoint is { } secondBoss &&
                    !chosen.coord.Equals(secondBoss.coord))
                {
                    current = chosen;
                    continue;
                }

                if (_runState.CurrentActIndex >= _runState.Acts.Count - 1)
                {
                    return CompleteRun(
                        won: true,
                        reachedBoss: true,
                        floorsVisited,
                        recorderFloorsVisited,
                        actsCleared: actsCleared + 1,
                        outcome: RunOutcome.Victory);
                }

                actsCleared++;
                _runState.AdvanceToNextAct();
                current = _runState.Map.StartingMapPoint;
                _runState.AddVisitedMapCoord(current.coord);
                if (current.PointType == MapPointType.Ancient)
                {
                    _recorder?.EnterFloor(current, RoomType.Event);
                    bool ancientProcessed = false;
                    try
                    {
                        await DriveAncientStartingRoomAsync();
                        ancientProcessed = true;
                    }
                    finally
                    {
                        if (ancientProcessed)
                        {
                            _recorder?.ExitFloor();
                        }
                    }

                    recorderFloorsVisited++;
                }

                // Ancient choices such as GoldenCompass may replace the generated map.
                current = _runState.Map.StartingMapPoint;

                if (_runState.IsGameOver)
                {
                    return CompleteRun(
                        won: false,
                        reachedBoss: true,
                        floorsVisited,
                        recorderFloorsVisited,
                        actsCleared: actsCleared,
                        outcome: RunOutcome.PlayerDefeated);
                }

                continue;
            }

            current = chosen;
        }

        return CompleteRun(
            won: false,
            reachedBoss: actsCleared > 0,
            floorsVisited,
            recorderFloorsVisited,
            truncated: !exhaustedMap,
            actsCleared: actsCleared,
            outcome: exhaustedMap ? RunOutcome.MapExhausted : RunOutcome.FloorLimitReached,
            mapExhaustedWithReachableChildren: exhaustedWithReachableChildren);
    }

    private Result CompleteRun(
        bool won,
        bool reachedBoss,
        int floorsVisited,
        int recorderFloorsVisited,
        bool truncated = false,
        int actsCleared = 0,
        RunOutcome outcome = RunOutcome.FloorLimitReached,
        bool mapExhaustedWithReachableChildren = false)
    {
        var result = new Result(won, reachedBoss, floorsVisited, PlayerHp())
        {
            Survived = !_runState.IsGameOver,
            Truncated = truncated,
            ActsCleared = actsCleared,
            Outcome = outcome,
            MapExhaustedWithReachableChildren = mapExhaustedWithReachableChildren,
        };
        _recorder?.EndRun(
            result.Won,
            recorderFloorsVisited,
            result.FinalPlayerHp,
            reachedBoss: result.ReachedBoss,
            survived: result.Survived,
            truncated: result.Truncated,
            actsCleared: result.ActsCleared);
        return result;
    }

    private int PlayerHp() => _runState.Players.Count > 0 ? (int)_runState.Players[0].Creature.CurrentHp : 0;

    private async Task DriveAncientStartingRoomAsync()
    {
        if (_runState.Map.StartingMapPoint.PointType != MapPointType.Ancient)
        {
            return;
        }

        AbstractRoom? previousRoom = _runState.CurrentRoom;
        AbstractRoom room = _createAncientEventRoom(_runState);
        NonCombatPlayerSnapshot? eventBefore = _recorder is not null && room is EventRoom
            ? NonCombatPlayerSnapshot.Capture(_runState.Players[0])
            : null;
        _runState.PushRoom(room);
        bool entered = false;
        try
        {
            await room.Enter(_runState);
            entered = true;
            if (room is EventRoom eventRoom)
            {
                FloorDetail? floorDetail = await DriveEventAsync(eventRoom, eventBefore, isAncient: true);
                if (floorDetail is not null)
                {
                    _recorder!.RecordFloorDetail(floorDetail);
                }
            }
        }
        finally
        {
            try
            {
                if (entered || room is EventRoom)
                {
                    await room.Exit(_runState);
                }
            }
            finally
            {
                while (_runState.CurrentRoom is { } currentRoom &&
                       !ReferenceEquals(currentRoom, previousRoom))
                {
                    _runState.PopCurrentRoom();
                }
            }
        }
    }

    private CombatRecordingObserver? ConfigureCombatObserver(CombatRoom combatRoom)
    {
        combatRoom.ConfigureCardSelectionSource(RunEngineCardSelectionDecisionSource.Instance);
        if (_recorder is null)
        {
            return null;
        }

        var observer = new CombatRecordingObserver(_recorder, _runState, combatRoom);
        combatRoom.ConfigureObserver(observer);
        return observer;
    }

    /// <summary>该目标类型是否必须拿到一个具体目标才能出牌。</summary>
    private static bool RequiresExplicitTarget(TargetType targetType) =>
        targetType is TargetType.AnyEnemy or TargetType.AnyAlly or TargetType.AnyPlayer;

    private async Task DriveCombatToCompletion(
        CombatRoom combatRoom,
        bool generateRewards = true,
        bool resolveRewards = true,
        CombatRecordingObserver? combatObserver = null)
    {
        CombatEngine engine = combatRoom.Engine;
        Player player = engine.State.Players[0];
        int playerTurns = 0;

        while (engine.IsInProgress)
        {
            if (_combatTurnLimit is { } limit && playerTurns >= limit)
            {
                throw new CombatTurnLimitExceededException(limit, combatRoom.RoomType);
            }

            playerTurns++;
            engine.CheckWinCondition();
            if (!engine.IsInProgress)
            {
                break;
            }

            if (_useAvailablePotions)
            {
                await CombatPotionPolicy.TryUseFirstAvailableAsync(engine, player);
                engine.CheckWinCondition();
                if (!engine.IsInProgress)
                {
                    break;
                }
            }

            bool playedCard;
            do
            {
                playedCard = false;
                foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
                {
                    if (!card.CanPlay(out _))
                    {
                        continue;
                    }

                    Creature? target = card.TargetType switch
                    {
                        TargetType.AnyEnemy => engine.State.HittableEnemies.FirstOrDefault(),
                        TargetType.AnyAlly or TargetType.AnyPlayer =>
                            CombatTargetCandidates.ForCard(engine.State, player, card.TargetType)
                                .FirstOrDefault(candidate => !ReferenceEquals(candidate, player.Creature))
                            ?? CombatTargetCandidates.ForCard(engine.State, player, card.TargetType)
                                .FirstOrDefault(),
                        _ => null,
                    };
                    // 偏离 #325（2026-09-09）：需要目标却解析不到时必须跳过这张卡。
                    //
                    // 上游 UnplayableReason 里同样没有"没有可打的敌人"这一项——权威把这个约束
                    // 放在呈现层：玩家点一张指向性卡，UI 强制他选一个合法目标，没得选就点不出去。
                    // 无头驱动没有那一层，于是 CanPlay 通过之后照打不误，target 为 null 直接把
                    // 卡自己的 ArgumentNullException.ThrowIfNull 打穿整局。
                    //
                    // 300 局不变量探针里这一类占 93 局（31%），散落在 Shiv/Slice/Skewer/
                    // Neutralize/DaggerThrow 等十余张指向性攻击卡上——是共性而非某张卡的缺陷。
                    if (target is null && RequiresExplicitTarget(card.TargetType))
                    {
                        continue;
                    }

                    await engine.PlayCardAsync(player, card, target);
                    engine.CheckWinCondition();
                    playedCard = true;
                    break;
                }
            }
            while (engine.IsInProgress && playedCard);

            if (engine.IsInProgress)
            {
                await engine.EndPlayerTurnAsync();
            }
        }

        await combatRoom.ResolveOutcomeAsync(generateRewards);
        combatObserver?.CaptureFinalState();
        if (resolveRewards)
        {
            foreach (RewardsSet rewards in combatRoom.GeneratedRewards)
            {
                await ResolveRewardsAsync(rewards);
            }
        }

        combatObserver?.EndCombat(engine.Won, combatRoom.GeneratedRewards);
    }

    private TreasureFloorDetail? CreateTreasureFloorDetail(TreasureRoom treasureRoom)
    {
        if (_recorder is null)
        {
            return null;
        }

        Player player = _runState.Players[0];
        TreasureRoomResolution resolution = treasureRoom.Resolutions
            .Single(candidate => ReferenceEquals(candidate.Player, player));
        return new TreasureFloorDetail(resolution.GoldGained, resolution.RelicGained);
    }

    private async Task<RestSiteFloorDetail?> DriveRestSiteToCompletion(RestSiteRoom restSiteRoom)     {         RestSiteFloorDetail? floorDetail = null;         foreach (Player player in _runState.Players)         {             restSiteRoom.GetAvailableDecisions(_runState, player);             while (restSiteRoom.HasRemainingDecisions(player))             {                 IReadOnlyList<RestSiteDecision> candidates =                     restSiteRoom.GetAvailableDecisions(_runState, player);                 if (candidates.Count == 0) break;                 RestSiteDecision decision = RestSiteDecisionPolicy.ChooseDefault(candidates);
                int hpBefore = player.Creature.CurrentHp;
                await restSiteRoom.ResolveAsync(player, decision);
                while (restSiteRoom.TryDequeuePendingRewardOffer(out RewardsSet? rewards))
                {
                    await ResolveRewardsAsync(rewards);
                }
                if (_recorder is not null)
                {
                    floorDetail = new RestSiteFloorDetail(
                        decision.GetType().Name,
                        decision is RestSiteDecision.Heal ? player.Creature.CurrentHp - hpBefore : null,
                        decision is RestSiteDecision.Smith smith ? smith.Card.Id.ToString() : null);
                }
            }
        }

        return floorDetail;
    }

    internal async Task<FloorDetail?> DriveEventAsync(
        EventRoom eventRoom,
        NonCombatPlayerSnapshot? before = null,
        bool isAncient = false)
    {
        try { return await DriveEventCoreAsync(eventRoom, before, isAncient); }
        finally { eventRoom.Event.EnsureCleanup(); }
    }

    private async Task<FloorDetail?> DriveEventCoreAsync(
        EventRoom eventRoom,
        NonCombatPlayerSnapshot? before,
        bool isAncient)
    {
        NonCombatPlayerSnapshot? effectiveBefore = before;
        if (_recorder is not null && effectiveBefore is null)
        {
            effectiveBefore = NonCombatPlayerSnapshot.Capture(eventRoom.Event.Owner);
        }
        var chosenOptionKeys = new List<string>();
        await DriveEventToCompletionWithForcedCombatAsync(eventRoom, chosenOptionKeys);
        await DrivePendingForcedCombatAsync(eventRoom);

        if (_recorder is null)
        {
            return null;
        }

        string eventName = eventRoom.Event.GetType().Name;
        string optionChosen = chosenOptionKeys.FirstOrDefault() ?? string.Empty;
        if (isAncient)
        {
            return new AncientFloorDetail(eventName, optionChosen);
        }

        return new EventFloorDetail(
            eventName,
            optionChosen,
            effectiveBefore!.DescribeChangesTo(eventRoom.Event.Owner));
    }

    private async Task<ForcedCombatOutcome> DrivePendingForcedCombatAsync(EventRoom eventRoom)
    {
        if (!eventRoom.Event.HasPendingForcedCombat)
        {
            return new ForcedCombatOutcome(Victory: false, TimedOut: false);
        }

        AbstractRoom? previousRoom = _runState.CurrentRoom;
        CombatRoom combatRoom = eventRoom.CreatePendingForcedCombatRoom();
        CombatRecordingObserver? combatObserver = ConfigureCombatObserver(combatRoom);
        _runState.PushRoom(combatRoom);
        bool entered = false;
        var outcome = new ForcedCombatOutcome(Victory: false, TimedOut: false);
        try
        {
            try
            {
                await combatRoom.Enter(_runState);
                entered = true;
            }
            catch
            {
                await combatRoom.RollbackFailedEnterAsync();
                throw;
            }
            var forcedEnemies = combatRoom.Engine.State.Enemies.ToHashSet();
            // 原版 EventCombatSynchronizer 只登记额外奖励，战后在普通奖励之后才 Populate；进战斗时掷骰会让 Rewards 流提前。
            foreach (Reward reward in eventRoom.Event.ForcedCombatExtraRewards)
                combatRoom.QueueExtraReward(eventRoom.Event.Owner, reward);
            var forcedTimeoutPowers = forcedEnemies
                .Select(enemy => enemy.GetPower<BattlewornDummyTimeLimitPower>())
                .OfType<BattlewornDummyTimeLimitPower>()
                .ToArray();
            await DriveCombatToCompletion(
                combatRoom,
                generateRewards: eventRoom.Event.GenerateForcedCombatRewards,
                resolveRewards: eventRoom.Event.GenerateForcedCombatRewards,
                combatObserver: combatObserver);
            bool timedOut = combatRoom.Engine.State.EscapedCreatures.Any(forcedEnemies.Contains)
                || forcedTimeoutPowers.Any(power => power.HasExpired);
            outcome = new ForcedCombatOutcome(Victory: combatRoom.Won, TimedOut: timedOut);
        }
        finally
        {
            try
            {
                if (entered)
                {
                    await combatRoom.Exit(_runState);
                }
            }
            finally
            {
                while (_runState.CurrentRoom is { } currentRoom &&
                       !ReferenceEquals(currentRoom, previousRoom))
                {
                    _runState.PopCurrentRoom();
                }
            }
        }

        return outcome;
    }

    private async Task DriveEventToCompletionWithForcedCombatAsync(
        EventRoom eventRoom,
        ICollection<string>? chosenOptionKeys = null)
    {
        for (int choices = 0; !eventRoom.Event.IsFinished; choices++)
        {
            if (choices >= MaximumEventChoices)
            {
                throw new InvalidOperationException("Event did not complete within the maximum number of choices.");
            }

            if (eventRoom.Event.IsAwaitingForcedCombat)
            {
                ForcedCombatOutcome outcome = await DrivePendingForcedCombatAsync(eventRoom);
                eventRoom.Event.ResumeAfterForcedCombat(outcome);
                await DrainEventRewardOffersAsync(eventRoom);
                continue;
            }

            IReadOnlyList<EventOption> options = eventRoom.Event.CurrentOptions;
            if (options.Count == 0)
            {
                CustomEventDecision action = await _chooseCustomEventAction(eventRoom.Event);
                await CustomEventDecisionPolicy.ExecuteAsync(eventRoom.Event, action);
                await DrainEventRewardOffersAsync(eventRoom);
                continue;
            }

            EventOption chosen = EventOptionDecisionPolicy.ChooseDefault(options);
            await eventRoom.Event.ChooseOption(chosen);
            chosenOptionKeys?.Add(chosen.Key);
            await DrainEventRewardOffersAsync(eventRoom);
        }

        await DrainEventRewardOffersAsync(eventRoom);
    }

    internal static async Task DriveEventToCompletion(
        EventRoom eventRoom,
        ICollection<string>? chosenOptionKeys = null)
    {
        for (int choices = 0; !eventRoom.Event.IsFinished; choices++)
        {
            if (choices >= MaximumEventChoices)
            {
                throw new InvalidOperationException("Event did not complete within the maximum number of choices.");
            }

            IReadOnlyList<EventOption> options = eventRoom.Event.CurrentOptions;
            if (options.Count == 0)
            {
                CustomEventDecision action = CustomEventDecisionPolicy.ChooseDefault(eventRoom.Event);
                await CustomEventDecisionPolicy.ExecuteAsync(eventRoom.Event, action);
                await DrainEventRewardOffersAsync(eventRoom);
                continue;
            }

            EventOption chosen = EventOptionDecisionPolicy.ChooseDefault(options);
            await eventRoom.Event.ChooseOption(chosen);
            chosenOptionKeys?.Add(chosen.Key);
            await DrainEventRewardOffersAsync(eventRoom);
        }

        await DrainEventRewardOffersAsync(eventRoom);
    }
    private static async Task DrainEventRewardOffersAsync(EventRoom eventRoom)
    {
        while (eventRoom.Event.TryDequeuePendingRewardOffer(out RewardsSet? rewards))
        {
            await ResolveRewardsAsync(rewards);
        }
    }

    private static async Task ResolveRewardsAsync(RewardsSet rewards)
    {
        EventRoom? eventRoom = rewards.Player.RunState.CurrentRoom as EventRoom;
        using IDisposable? nestedOffers = eventRoom?.Event.BeginNestedRewardOffers();
        await rewards.Gold.Take();
        await DrainNestedRewardOffersAsync(rewards.Player);
        await TakeExtraRewardsAsync<GoldReward>(rewards);
        if (rewards.Potion is not null)
        {
            await rewards.Potion.Take();
            await DrainNestedRewardOffersAsync(rewards.Player);
        }
        await TakeExtraRewardsAsync<PotionReward>(rewards);
        if (rewards.Relic is not null)
        {
            await rewards.Relic.Take();
            await DrainNestedRewardOffersAsync(rewards.Player);
        }
        await TakeExtraRewardsAsync<RelicReward>(rewards);
        CardModel? cardPick = rewards.Card.Options.FirstOrDefault(card => !CrossCharacterContentExclusions.IsExcluded(card));
        if (cardPick is not null)
        {
            await rewards.Card.SelectOption(cardPick);
        }
        else
        {
            await rewards.Card.Skip();
        }

        await DrainNestedRewardOffersAsync(rewards.Player);
        foreach (Reward extraReward in rewards.ExtraRewards)
        {
            switch (extraReward)
            {
                case TakeableReward takeableReward:
                    await TakeExtraRewardAsync(takeableReward);
                    break;
                case CardReward cardReward:
                    if (cardReward.IsResolved)
                    {
                        break;
                    }
                    CardModel? extraCardPick = cardReward.Options
                        .FirstOrDefault(card => !CrossCharacterContentExclusions.IsExcluded(card));
                    if (extraCardPick is not null)
                    {
                        await cardReward.SelectOption(extraCardPick);
                    }
                    else
                    {
                        await cardReward.Skip();
                    }
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported extra reward type: {extraReward.GetType().Name}.");
            }
            await DrainNestedRewardOffersAsync(rewards.Player);
        }
    }

    private static async Task TakeExtraRewardsAsync<TReward>(RewardsSet rewards)
        where TReward : TakeableReward
    {
        foreach (TReward reward in rewards.ExtraRewards.OfType<TReward>())
            await TakeExtraRewardAsync(reward);
    }

    private static async Task TakeExtraRewardAsync(TakeableReward reward)
    {
        if (reward.IsResolved) return;
        if (reward is PotionReward { IsOptionalMerchantChoice: true, CanTake: false })
            await reward.Skip();
        else await reward.Take();
        await DrainNestedRewardOffersAsync(reward.Player);
    }

    private static Task DrainNestedRewardOffersAsync(Player player) =>
        player.RunState.CurrentRoom is EventRoom eventRoom
            ? DrainEventRewardOffersAsync(eventRoom)
            : Task.CompletedTask;
}
