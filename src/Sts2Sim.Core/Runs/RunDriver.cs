using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Rewards;
using System.Runtime.CompilerServices;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// Pull-style counterpart to <see cref="RunEngine"/>. Map and combat choices are supplied by an
/// external decision source while the underlying map, room, and combat engines remain shared.
/// </summary>
public sealed class RunDriver
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
        /// **为真即引擎缺陷**——有路却取不到，正是偏离 #320 的形态。</summary>
        public bool MapExhaustedWithReachableChildren { get; init; }
    }

    private const int MaximumEventChoices = 100;

    private readonly RunState _runState;
    private readonly IRunDecisionSource _decisionSource;
    private readonly Func<RunState, AbstractRoom> _createAncientEventRoom;
    private readonly Func<RunState, AbstractRoom> _createEventRoom;
    private readonly IRunRecorder? _recorder;
    private readonly bool _useAvailablePotions;

    [OverloadResolutionPriority(1)]
    public RunDriver(
        RunState runState,
        IRunDecisionSource decisionSource,
        Func<RunState, AbstractRoom>? createAncientEventRoom = null)
        : this(
            runState,
            decisionSource,
            createAncientEventRoom,
            recorder: null,
            useAvailablePotions: false,
            createEventRoom: null)
    {
    }

    public RunDriver(
        RunState runState,
        IRunDecisionSource decisionSource,
        Func<RunState, AbstractRoom>? createAncientEventRoom = null,
        IRunRecorder? recorder = null,
        bool useAvailablePotions = false)
        : this(
            runState,
            decisionSource,
            createAncientEventRoom,
            recorder,
            useAvailablePotions,
            createEventRoom: null)
    {
    }

    public RunDriver(
        RunState runState,
        IRunDecisionSource decisionSource,
        Func<RunState, AbstractRoom>? createAncientEventRoom,
        IRunRecorder? recorder,
        bool useAvailablePotions,
        Func<RunState, AbstractRoom>? createEventRoom)
    {
        _runState = runState;
        _decisionSource = decisionSource;
        if (runState is not null && decisionSource is not null)
        {
            _runState.ConfigureCardSelectionSource(_decisionSource);
        }
        _createAncientEventRoom = createAncientEventRoom ?? RoomFactory.CreateAncientEventRoom;
        _createEventRoom = createEventRoom ?? (state => RoomFactory.CreateRoom(state, RoomType.Event));
        _recorder = recorder;
        _useAvailablePotions = useAvailablePotions;
    }

    public event Action<MapPoint, RoomType>? OnRoomResolved;

    public Task<Result> RunAsync(int maxFloors) => RunCoreAsync(maxFloors, continueFromCurrentState: false);

    /// <summary>Continue the current displayed state without re-entering a completed Starting/Ancient room.</summary>
    public Task<Result> ContinueAsync(int maxFloors = int.MaxValue) =>
        RunCoreAsync(maxFloors, continueFromCurrentState: true);

    private async Task<Result> RunCoreAsync(int maxFloors, bool continueFromCurrentState)
    {
        _recorder?.BeginRun(_runState);

        int recorderFloorsVisited = 0;
        if (_runState.IsGameOver)
        {
            return CompleteRun(won: false, reachedBoss: false, floorsVisited: 0, recorderFloorsVisited,
                outcome: RunOutcome.AlreadyOver);
        }

        bool startingAlreadyEntered = continueFromCurrentState && _runState.VisitedMapCoords.Count > 0;
        if (!startingAlreadyEntered)
        {
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
        MapPoint current = continueFromCurrentState
            ? _runState.CurrentMapPoint ?? _runState.Map.StartingMapPoint
            : _runState.Map.StartingMapPoint;
        AbstractRoom? resumedRoom = continueFromCurrentState ? _runState.CurrentRoom : null;

        while (floorsVisited < maxFloors)
        {
            MapPoint chosen;
            RoomType roomType;
            AbstractRoom room;
            NonCombatPlayerSnapshot? eventBefore = null;
            CombatRecordingObserver? combatObserver = null;
            bool resumingRoom = resumedRoom is not null;
            if (resumingRoom)
            {
                chosen = current;
                room = resumedRoom!;
                resumedRoom = null;
                roomType = room.RoomType;
                _recorder?.EnterFloor(chosen, roomType);
                floorsVisited++;
                await ResumeActiveRewardOffersAsync();
                if (room is CombatRoom completedCombat)
                {
                    foreach (RewardsSet rewards in completedCombat.GeneratedRewards)
                        if (HasUnresolvedRewards(rewards)) await ResolveRewardsAsync(rewards);
                    if (_runState.CurrentRoomCount > 1 && _runState.BaseRoom is EventRoom parentEvent)
                    {
                        await room.Exit(_runState);
                        _runState.PopCurrentRoom();
                        if (_runState.ForcedCombatResumeOutcome is { } outcome)
                            parentEvent.Event.ResumeAfterForcedCombat(outcome);
                        _runState.ForcedCombatResumeOutcome = null;
                        room = parentEvent;
                        roomType = room.RoomType;
                    }
                }
            }
            else
            {
                IReadOnlyList<MapPoint> travelable = MapTravel.GetTravelablePointsFrom(_runState, current).ToList();
                if (travelable.Count == 0)
                {
                    exhaustedMap = true;
                    // 取点入口给了空集合，但当前点可能其实还连着子节点——那是取点逻辑的缺陷
                    // 而不是真的走到头（偏离 #320）。在这里留证据，供探针机器判定。
                    exhaustedWithReachableChildren = current.Children.Count > 0;
                    break;
                }

                chosen = await _decisionSource.ChooseMapPointAsync(travelable);
                if (!travelable.Contains(chosen))
                {
                    throw new InvalidOperationException("Decision source returned a point outside the current map options.");
                }

                _runState.AddVisitedMapCoord(chosen.coord);
                floorsVisited++;

                UnknownMapPointPublicRules? visibleUnknownRules = chosen.PointType == MapPointType.Unknown
                    ? _runState.CaptureVisibleUnknownMapPointRules(chosen) : null;
                roomType = RoomFactory.ResolveRoomType(_runState, chosen);
                _recorder?.EnterFloor(chosen, roomType);
                eventBefore = _recorder is not null && roomType == RoomType.Event
                    ? NonCombatPlayerSnapshot.Capture(_runState.Players[0])
                    : null;
                room = roomType == RoomType.Event
                    ? _createEventRoom(_runState)
                    : RoomFactory.CreateRoom(_runState, roomType);
                combatObserver = room is CombatRoom combatToObserve
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
                if (visibleUnknownRules is { } rules)
                    _runState.RecordVisibleUnknownMapPointEntry(roomType, rules);
            }
            FloorDetail? floorDetail = null;
            if (room is CombatRoom combatRoom)
            {
                if (!resumingRoom)
                    await DriveCombatAsync(combatRoom, combatObserver: combatObserver);
            }
            else if (room is MerchantRoom merchantRoom)
            {
                floorDetail = await DriveShopAsync(merchantRoom);
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
                floorDetail = await DriveRestSiteAsync(restSiteRoom);
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
                return CompleteRun(
                    won: false,
                    reachedBoss: chosen.PointType == MapPointType.Boss,
                    floorsVisited,
                    recorderFloorsVisited,
                    actsCleared: actsCleared,
                    outcome: RunOutcome.PlayerDefeated);
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

    /// <summary>Continue exactly one known combat from a between-room transplant state.</summary>
    public async Task<CombatRoom> RunOneCombatFromTransplantAsync(
        MapPoint target, Action<CombatRoom, CombatState>? beforeCombatSetup = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (_runState.CurrentRoom is not null || _runState.Players.Count != 1)
            throw new InvalidOperationException("Transplant combat requires one player between rooms.");
        MapPoint? mapped = _runState.Map.GetPoint(target.coord);
        if (!ReferenceEquals(mapped, target) || _runState.VisitedMapCoords.Contains(target.coord))
            throw new InvalidOperationException("Target is not an unvisited point on the current map.");
        MapPoint? current = _runState.CurrentMapPoint;
        if (current is null || !MapTravel.GetTravelablePointsFrom(_runState, current).Contains(target))
            throw new InvalidOperationException("Target is not travelable from the imported map position.");
        _runState.AddVisitedMapCoord(target.coord);
        RoomType type = RoomFactory.ResolveRoomType(_runState, target);
        if (type is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
            throw new InvalidOperationException("Transplant target did not resolve to a combat room.");
        _recorder?.EnterFloor(target, type);
        CombatRoom room = (CombatRoom)RoomFactory.CreateRoom(_runState, type);
        if (beforeCombatSetup is not null) room.ConfigureBeforeSetupDiagnostic(beforeCombatSetup);
        CombatRecordingObserver? observer = ConfigureCombatObserver(room);
        _runState.PushRoom(room);
        await room.Enter(_runState);
        await DriveCombatAsync(room, generateRewards: true, resolveRewards: false,
            combatObserver: observer);
        // The normal driver asks for reward decisions before Exit/Pop. Stop at that boundary.
        return room;
    }
    /// <summary>Drive one host-injected combat without a map point or map travel.</summary>
    public async Task<CombatRoom> RunOneInjectedCombatAsync(
        RoomType roomType, string encounterId,
        Action<CombatRoom, CombatState>? beforeCombatSetup = null)
    {
        if (_runState.CurrentRoom is not null || _runState.Players.Count != 1)
            throw new InvalidOperationException("Injected combat requires one player between rooms.");
        if (roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(roomType));
        _runState.SetInjectedEncounterForTransplant(roomType, encounterId);
        CombatRoom room = (CombatRoom)RoomFactory.CreateRoom(_runState, roomType);
        if (beforeCombatSetup is not null) room.ConfigureBeforeSetupDiagnostic(beforeCombatSetup);
        CombatRecordingObserver? observer = ConfigureCombatObserver(room);
        _runState.PushRoom(room);
        await room.Enter(_runState);
        await DriveCombatAsync(room, generateRewards: true, resolveRewards: false,
            combatObserver: observer);
        return room;
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
        combatRoom.ConfigureCardSelectionSource(_decisionSource);
        if (_recorder is null)
        {
            return null;
        }

        var observer = new CombatRecordingObserver(_recorder, _runState, combatRoom);
        combatRoom.ConfigureObserver(observer);
        return observer;
    }

    private async Task DriveCombatAsync(
        CombatRoom combatRoom,
        bool generateRewards = true,
        bool resolveRewards = true,
        CombatRecordingObserver? combatObserver = null,
        IReadOnlySet<Sts2Sim.Core.Entities.Creatures.Creature>? forcedEnemies = null,
        IReadOnlyList<BattlewornDummyTimeLimitPower>? forcedTimeoutPowers = null)
    {
        CombatEngine engine = combatRoom.Engine;
        Player player = engine.State.Players[0];
        int lastPotionTurn = -1;

        while (engine.IsInProgress)
        {
            engine.CheckWinCondition();
            if (!engine.IsInProgress)
            {
                break;
            }

            int turnNumber = player.PlayerCombatState!.TurnNumber;
            if (_useAvailablePotions && turnNumber != lastPotionTurn)
            {
                lastPotionTurn = turnNumber;
                await CombatPotionPolicy.TryUseFirstAvailableAsync(engine, player);
                engine.CheckWinCondition();
                if (!engine.IsInProgress)
                {
                    continue;
                }
            }

            CombatDecision decision = await _decisionSource.ChooseCombatActionAsync(engine.State);
            switch (decision)
            {
                case CombatDecision.PlayCard playCard:
                    ValidatePlayCard(engine.State, player, playCard);
                    await engine.PlayCardAsync(player, playCard.Card, playCard.Target);
                    engine.CheckWinCondition();
                    break;
                case CombatDecision.UsePotion usePotion:
                    await PotionCmd.Use(usePotion.Potion, player, usePotion.Target);
                    engine.CheckWinCondition();
                    break;
                case CombatDecision.EndTurn:
                    if (engine.IsInProgress)
                    {
                        await engine.EndPlayerTurnAsync();
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled combat decision: {decision}");
            }
        }

        (_decisionSource as ICombatCompletionValidator)?.ValidateCompletedCombat(engine.State);
        await combatRoom.ResolveOutcomeAsync(generateRewards);
        if (forcedEnemies is not null)
            _runState.ForcedCombatResumeOutcome = new ForcedCombatOutcome(engine.Won,
                engine.State.EscapedCreatures.Any(forcedEnemies.Contains) ||
                (forcedTimeoutPowers?.Any(power => power.HasExpired) ?? false));
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

    private async Task<RestSiteFloorDetail?> DriveRestSiteAsync(RestSiteRoom restSiteRoom)     {         RestSiteFloorDetail? floorDetail = null;         foreach (Player player in _runState.Players)         {             restSiteRoom.GetAvailableDecisions(_runState, player);             while (restSiteRoom.HasRemainingDecisions(player))             {                 IReadOnlyList<RestSiteDecision> candidates =                     restSiteRoom.GetAvailableDecisions(_runState, player);                 if (candidates.Count == 0) break;                 RestSiteDecision decision = await _decisionSource.ChooseRestSiteActionAsync(player, candidates);
                if (!RestSiteDecisionPolicy.Contains(candidates, decision))
                {
                    throw new InvalidOperationException(
                        "Decision source returned a rest-site choice outside the current candidate snapshot.");
                }
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
                CustomEventDecision action = await _decisionSource.ChooseCustomEventActionAsync(eventRoom.Event);
                await CustomEventDecisionPolicy.ExecuteAsync(eventRoom.Event, action);
                await DrainEventRewardOffersAsync(eventRoom);
                continue;
            }

            EventOption chosen = await _decisionSource.ChooseEventOptionAsync(options);
            if (!eventRoom.Event.CurrentOptions.Any(option => ReferenceEquals(option, chosen)))
            {
                throw new InvalidOperationException("Decision source returned an option outside the current event options.");
            }

            await eventRoom.Event.ChooseOption(chosen);
            chosenOptionKeys.Add(chosen.Key);
            await DrainEventRewardOffersAsync(eventRoom);
        }

        await DrainEventRewardOffersAsync(eventRoom);
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
            await DriveCombatAsync(
                combatRoom,
                generateRewards: eventRoom.Event.GenerateForcedCombatRewards,
                resolveRewards: eventRoom.Event.GenerateForcedCombatRewards,
                combatObserver: combatObserver,
                forcedEnemies: forcedEnemies,
                forcedTimeoutPowers: forcedTimeoutPowers);
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

        _runState.ForcedCombatResumeOutcome = null;
        return outcome;
    }

    private async Task DrainEventRewardOffersAsync(EventRoom eventRoom)
    {
        while (eventRoom.Event.TryDequeuePendingRewardOffer(out RewardsSet? rewards))
        {
            await ResolveRewardsAsync(rewards);
        }
    }
    private async Task<ShopFloorDetail?> DriveShopAsync(MerchantRoom shop)
    {
        Player player = _runState.Players[0];
        MerchantInventory inventory = shop.Inventory;
        int maximumPurchases = inventory.Cards.Count + inventory.Relics.Count + inventory.Potions.Count + 1;
        List<PurchaseRecord>? purchaseRecords = _recorder is null ? null : [];

        for (int purchases = 0; ; purchases++)
        {
            while (shop.TryDequeuePendingRewardOffer(out RewardsSet? rewards)) await ResolveRewardsAsync(rewards);
            ShopDecision decision = await _decisionSource.ChooseShopActionAsync(inventory, player);
            if (decision is ShopDecision.Leave)
            {
                return purchaseRecords is null ? null : new ShopFloorDetail(purchaseRecords);
            }

            bool hasRefillingShelf = inventory.Cards.Cast<MerchantEntry>()
                .Concat(inventory.Relics)
                .Concat(inventory.Potions)
                .Any(entry => Hook.ShouldRefillMerchantEntry(_runState, entry, player));

            if (purchases >= maximumPurchases && !hasRefillingShelf)
            {
                throw new InvalidOperationException("Decision source did not leave after every shop item was purchased.");
            }

            switch (decision)
            {
                case ShopDecision.BuyCard buyCard:
                    ValidateShopPurchase(buyCard.Entry, inventory.Cards, player);
                    int cardPrice = await shop.BuyWithPriceAsync(buyCard.Entry, player);
                    purchaseRecords?.Add(new PurchaseRecord("Card", buyCard.Entry.Card.Id.ToString(), cardPrice));
                    break;
                case ShopDecision.BuyRelic buyRelic:
                    ValidateShopPurchase(buyRelic.Entry, inventory.Relics, player);
                    int relicPrice = await shop.BuyWithPriceAsync(buyRelic.Entry, player);
                    purchaseRecords?.Add(new PurchaseRecord("Relic", buyRelic.Entry.Relic.Id.ToString(), relicPrice));
                    break;
                case ShopDecision.BuyPotion buyPotion:
                    ValidateShopPurchase(buyPotion.Entry, inventory.Potions, player);
                    int potionPrice = await shop.BuyWithPriceAsync(buyPotion.Entry, player);
                    purchaseRecords?.Add(new PurchaseRecord("Potion", buyPotion.Entry.Potion.Id.ToString(), potionPrice));
                    break;
                case ShopDecision.BuyCardRemoval buyCardRemoval:
                    ValidateShopPurchase(inventory.CardRemoval, new[] { inventory.CardRemoval }, player);
                    if (!player.Deck.Cards.Any(card => ReferenceEquals(card, buyCardRemoval.CardToRemove)))
                    {
                        throw new InvalidOperationException("Card to remove is not in the player's deck.");
                    }
                    int removalPrice = await shop.BuyCardRemovalWithPriceAsync(buyCardRemoval.CardToRemove, player);
                    purchaseRecords?.Add(new PurchaseRecord(
                        "CardRemoval",
                        buyCardRemoval.CardToRemove.Id.ToString(),
                        removalPrice));
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled shop decision: {decision}");
            }
        }
    }

    private static void ValidateShopPurchase<TEntry>(
        TEntry entry,
        IReadOnlyList<TEntry> options,
        Player player)
        where TEntry : MerchantEntry
    {
        if (entry is null || !options.Any(option => ReferenceEquals(option, entry)))
        {
            throw new InvalidOperationException("Decision source returned an item outside the current merchant inventory.");
        }

        if (entry.Purchased)
        {
            throw new InvalidOperationException("This item has already been purchased.");
        }

        if (player.Gold < entry.Price)
        {
            throw new InvalidOperationException("Not enough gold for this purchase.");
        }
    }

    private async Task ResolveRewardsAsync(RewardsSet rewards, bool resumeFrame = false)
    {
        EventRoom? eventRoom = _runState.CurrentRoom as EventRoom;
        if (!resumeFrame) _runState.BeginActiveRewardOffer(rewards);
        try
        {
            using IDisposable? nestedOffers = eventRoom is null ? null
                : resumeFrame ? eventRoom.Event.ResumeNestedRewardOffers()
                : eventRoom.Event.BeginNestedRewardOffers();
            while (true)
            {
                RewardDecision decision = await _decisionSource.ChooseRewardActionAsync(rewards);
                switch (decision)
                {
                    case RewardDecision.TakeGold:
                        if (rewards.Gold.IsResolved)
                        {
                            throw new InvalidOperationException("Gold reward has already been resolved.");
                        }
                        await rewards.Gold.Take();
                        break;
                    case RewardDecision.TakePotion:
                        if (rewards.Potion is null || rewards.Potion.IsResolved)
                        {
                            throw new InvalidOperationException("No unresolved potion reward is available.");
                        }
                        await rewards.Potion.Take();
                        break;
                    case RewardDecision.TakeRelic:
                        if (rewards.Relic is null || rewards.Relic.IsResolved)
                        {
                            throw new InvalidOperationException("No unresolved relic reward is available.");
                        }
                        await rewards.Relic.Take();
                        break;
                    case RewardDecision.TakeCard takeCard:
                        if (rewards.Card.IsResolved)
                        {
                            throw new InvalidOperationException("Card reward has already been resolved.");
                        }
                        if (!rewards.Card.Options.Any(option => ReferenceEquals(option, takeCard.Card)))
                        {
                            throw new InvalidOperationException("Decision source returned a card outside the current reward options.");
                        }
                        await rewards.Card.SelectOption(takeCard.Card);
                        break;
                    case RewardDecision.SkipCard:
                        if (rewards.Card.IsResolved)
                        {
                            throw new InvalidOperationException("Card reward has already been resolved.");
                        }
                        await rewards.Card.Skip();
                        break;
                    case RewardDecision.SelectCardAlternative alternative:
                        if ((!ReferenceEquals(rewards.Card, alternative.Reward) &&
                             !rewards.ExtraRewards.Any(reward => ReferenceEquals(reward, alternative.Reward))) ||
                            alternative.Reward.IsResolved)
                            throw new InvalidOperationException("No matching unresolved card reward is available.");
                        await alternative.Reward.SelectAlternative(alternative.Alternative);
                        break;
                    case RewardDecision.ResolveExtra resolveExtra:
                        await ResolveExtraRewardAsync(rewards, resolveExtra);
                        break;
                    case RewardDecision.Done:
                        if (HasUnresolvedRewards(rewards))
                        {
                            throw new InvalidOperationException("Cannot finish reward selection while unresolved rewards remain.");
                        }
                        return;
                    default:
                        throw new InvalidOperationException($"Unhandled reward decision: {decision}");
                }
                if (eventRoom is not null)
                    await DrainEventRewardOffersAsync(eventRoom);
            }
        }
        finally
        {
            _runState.EndActiveRewardOffer();
        }
    }

    private async Task ResumeActiveRewardOffersAsync()
    {
        while (_runState.ActiveRewardOffers.Count > 0)
        {
            RewardsSet offer = _runState.ActiveRewardOffers[^1];
            await ResolveRewardsAsync(offer, resumeFrame: true);
            if (_runState.CurrentRoom is EventRoom eventRoom)
                await DrainEventRewardOffersAsync(eventRoom);
        }
    }

    private static async Task ResolveExtraRewardAsync(
        RewardsSet rewards,
        RewardDecision.ResolveExtra decision)
    {
        Reward? offeredReward = rewards.ExtraRewards.FirstOrDefault(
            reward => ReferenceEquals(reward, decision.Reward));
        if (offeredReward is null)
        {
            throw new InvalidOperationException(
                "Decision source returned a reward outside the current extra rewards.");
        }

        if (offeredReward.IsResolved)
        {
            throw new InvalidOperationException("Extra reward has already been resolved.");
        }

        switch (offeredReward)
        {
            case TakeableReward takeableReward:
                if (decision.SelectedCard is not null)
                {
                    throw new InvalidOperationException(
                        "Takeable extra rewards do not accept a card selection.");
                }
                if (decision.Skip)
                {
                    if (takeableReward is not (PotionReward { IsOptionalMerchantChoice: true } or CardRemovalReward))
                        throw new InvalidOperationException(
                            "Only optional merchant potion and card removal rewards can be skipped here.");
                    await takeableReward.Skip();
                }
                else await takeableReward.Take();
                return;
            case CardReward cardReward:
                if (decision.Skip) throw new InvalidOperationException("Card reward skips use an empty card selection.");
                if (decision.SelectedCard is null)
                {
                    await cardReward.Skip();
                    return;
                }
                if (!cardReward.Options.Any(option => ReferenceEquals(option, decision.SelectedCard)))
                {
                    throw new InvalidOperationException(
                        "Decision source returned a card outside the selected extra card reward options.");
                }
                await cardReward.SelectOption(decision.SelectedCard);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported extra reward type: {offeredReward.GetType().Name}.");
        }
    }

    private static bool HasUnresolvedRewards(RewardsSet rewards) =>
        !rewards.Gold.IsResolved ||
        rewards.Potion is { IsResolved: false } ||
        rewards.Relic is { IsResolved: false } ||
        !rewards.Card.IsResolved ||
        rewards.ExtraRewards.Any(reward => !reward.IsResolved);

    private static void ValidatePlayCard(
        CombatState state,
        Player player,
        CombatDecision.PlayCard playCard)
    {
        if (!player.PlayerCombatState!.Hand.Cards.Contains(playCard.Card))
        {
            throw new InvalidOperationException("Decision source returned a card outside the current hand.");
        }

        if (!playCard.Card.CanPlay(out _))
        {
            throw new InvalidOperationException("Decision source returned a card that cannot currently be played.");
        }

        if (playCard.Card.TargetType == TargetType.AnyEnemy)
        {
            if (playCard.Target is null || !state.HittableEnemies.Contains(playCard.Target))
            {
                throw new InvalidOperationException("Decision source returned a target that is not a hittable enemy.");
            }

            return;
        }

        if (playCard.Card.TargetType is TargetType.AnyAlly or TargetType.AnyPlayer)
        {
            if (!CombatTargetCandidates.IsLegalCardTarget(
                    state, player, playCard.Card, playCard.Target))
            {
                throw new InvalidOperationException(
                    "Decision source returned a target that is not a legal living player target.");
            }
            return;
        }

        if (playCard.Target is not null)
        {
            throw new InvalidOperationException("Decision source returned a target for a card that does not use an enemy target.");
        }
    }
}
