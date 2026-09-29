using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Runs;

public sealed class RunDriverRecorderTests : IDisposable
{
    public RunDriverRecorderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunAsync_WithNullRecorder_MatchesOmittedRecorderFullDeterministicTrace()
    {
        RunCapture omitted = await RunOnceAsync(
            recorder: null,
            passRecorderArgument: false,
            maxFloors: 1,
            startingPointType: MapPointType.Ancient,
            forceFirstTravelRoomMonster: true);
        RunCapture explicitNull = await RunOnceAsync(
            recorder: null,
            passRecorderArgument: true,
            maxFloors: 1,
            startingPointType: MapPointType.Ancient,
            forceFirstTravelRoomMonster: true);

        Assert.Equal(omitted.Result, explicitNull.Result);
        Assert.Equal(omitted.VisitedMapCoords, explicitNull.VisitedMapCoords);
        Assert.Equal(omitted.Decisions, explicitNull.Decisions);
        Assert.Equal(omitted.ResolvedRooms, explicitNull.ResolvedRooms);
        Assert.Equal(omitted.RunRngCounters, explicitNull.RunRngCounters);
        Assert.Equal(omitted.PlayerHp, explicitNull.PlayerHp);
        Assert.Equal(omitted.PlayerGold, explicitNull.PlayerGold);
        Assert.Equal(omitted.Deck, explicitNull.Deck);
        Assert.Equal(omitted.Relics, explicitNull.Relics);
        Assert.Equal(omitted.Potions, explicitNull.Potions);
        Assert.Equal(omitted.PlayerCombatStateCleared, explicitNull.PlayerCombatStateCleared);
        Assert.Equal(omitted.CreatureCombatStateCleared, explicitNull.CreatureCombatStateCleared);
        Assert.Null(omitted.CurrentRoom);
        Assert.Null(explicitNull.CurrentRoom);
    }

    [Fact]
    public async Task RunAsync_RecordsAncientFloorLifecycleAndAllowsDetailDuringAncientProcessing()
    {
        var recorder = new RecordingRunRecorder();
        bool ancientProcessingObserved = false;
        RunCapture capture = await RunOnceAsync(
            recorder,
            passRecorderArgument: true,
            maxFloors: 0,
            startingPointType: MapPointType.Ancient,
            createAncientEventRoom: _ => new EventRoom(() =>
                (EventModel)new StubAncientEvent(() =>
                {
                    Assert.True(recorder.IsFloorActive);
                    ancientProcessingObserved = true;
                }).MutableClone()));

        Assert.True(ancientProcessingObserved);
        Assert.Equal(1, recorder.FloorDetailCount);
        Assert.Equal(0, capture.Result.FloorsVisited);
        Assert.Equal(["BeginRun", "EnterFloor", "ExitFloor", "EndRun"], recorder.Calls);
        FloorEntry floor = Assert.Single(recorder.Floors);
        Assert.Equal(capture.VisitedMapCoords[0], floor.Point.coord);
        Assert.Equal(RoomType.Event, floor.RoomType);
        Assert.Equal((false, 1, capture.Result.FinalPlayerHp), recorder.EndRunSummary);
    }

    [Fact]
    public async Task RunAsync_PreservesAncientProcessingExceptionWhenRecorderExitWouldThrow()
    {
        var recorder = new RecordingRunRecorder { ThrowOnExit = true };
        RunState runState = NewRunState("run-driver-recorder-ancient-exception", MapPointType.Ancient);
        var driver = new RunDriver(
            runState,
            new RecordingDecisionSource(),
            createAncientEventRoom: _ => new ThrowingStartingRoom(),
            recorder: recorder);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 0));

        Assert.Equal("Ancient processing failed.", exception.Message);
        Assert.Equal(["BeginRun", "EnterFloor"], recorder.Calls);
        Assert.Null(recorder.EndRunSummary);
    }

    [Fact]
    public async Task RunAsync_RecordsBalancedAncientFloorLifecycleWhenAncientRoomIsLethal()
    {
        var recorder = new RecordingRunRecorder();
        RunCapture capture = await RunOnceAsync(
            recorder,
            passRecorderArgument: true,
            maxFloors: 1,
            startingPointType: MapPointType.Ancient,
            createAncientEventRoom: _ => new LethalStartingRoom());

        Assert.True(capture.IsGameOver);
        Assert.False(capture.Result.Won);
        Assert.Equal(0, capture.Result.FloorsVisited);
        Assert.Equal(["BeginRun", "EnterFloor", "ExitFloor", "EndRun"], recorder.Calls);
        Assert.Single(recorder.Floors);
        Assert.Equal((false, 1, 0), recorder.EndRunSummary);
    }

    [Fact]
    public async Task RunAsync_RecordsBalancedFloorLifecycleAndEndRunOnNormalCompletion()
    {
        var recorder = new RecordingRunRecorder();
        RunCapture capture = await RunOnceAsync(recorder, passRecorderArgument: true, maxFloors: 1);

        Assert.False(capture.Result.Won);
        Assert.False(capture.IsGameOver);
        Assert.Equal(1, capture.Result.FloorsVisited);
        Assert.Same(capture.RunState, recorder.BegunRunState);
        FloorEntry floor = Assert.Single(recorder.Floors);
        Assert.Equal(capture.VisitedMapCoords[1], floor.Point.coord);
        Assert.Equal(capture.ResolvedRooms[0].RoomType, floor.RoomType);
        Assert.Equal(["BeginRun", "EnterFloor", "ExitFloor", "EndRun"], recorder.Calls);
        Assert.Equal((capture.Result.Won, capture.Result.FloorsVisited, capture.Result.FinalPlayerHp), recorder.EndRunSummary);
        Assert.True(recorder.CombatCallCount > 0);
        Assert.Equal(1, recorder.CombatEndCount);
    }

    [Fact]
    public async Task RunAsync_RecordsBalancedFloorLifecycleAndEndRunWhenDefeated()
    {
        var recorder = new RecordingRunRecorder();
        RunCapture capture = await RunOnceAsync(
            recorder,
            passRecorderArgument: true,
            maxFloors: 1,
            defeatAfterFirstRoom: true);

        Assert.False(capture.Result.Won);
        Assert.True(capture.IsGameOver);
        Assert.Equal(1, capture.Result.FloorsVisited);
        Assert.Single(recorder.Floors);
        Assert.Equal(["BeginRun", "EnterFloor", "ExitFloor", "EndRun"], recorder.Calls);
        Assert.Equal((false, 1, 0), recorder.EndRunSummary);
        Assert.True(recorder.CombatCallCount > 0);
        Assert.Equal(1, recorder.CombatEndCount);
    }

    [Fact]
    public async Task RunAsync_PreservesPullDecisionExceptionWithoutExitingIncompleteFloor()
    {
        var recorder = new RecordingRunRecorder { ThrowOnExit = true };
        RunState runState = NewRunState("run-driver-recorder-floor-exception", MapPointType.Monster);
        FirstTravelPoint(runState).PointType = MapPointType.RestSite;
        var driver = new RunDriver(
            runState,
            new ThrowingRestSiteDecisionSource(),
            createAncientEventRoom: null,
            recorder: recorder);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        Assert.Equal("Rest-site decision failed.", exception.Message);
        Assert.Equal(["BeginRun", "EnterFloor"], recorder.Calls);
        Assert.Null(recorder.EndRunSummary);
    }

    private static async Task<RunCapture> RunOnceAsync(
        IRunRecorder? recorder,
        bool passRecorderArgument,
        int maxFloors = 5,
        bool defeatAfterFirstRoom = false,
        MapPointType startingPointType = MapPointType.Monster,
        bool forceFirstTravelRoomMonster = false,
        Func<RunState, AbstractRoom>? createAncientEventRoom = null)
    {
        RunState runState = NewRunState("run-driver-recorder", startingPointType);
        if (forceFirstTravelRoomMonster)
        {
            FirstTravelPoint(runState).PointType = MapPointType.Monster;
        }

        Player player = runState.Players[0];
        var decisionSource = new RecordingDecisionSource();
        var resolvedRooms = new List<(MapCoord Point, RoomType RoomType)>();
        RunDriver driver = passRecorderArgument
            ? new RunDriver(runState, decisionSource, createAncientEventRoom, recorder)
            : new RunDriver(runState, decisionSource, createAncientEventRoom);
        driver.OnRoomResolved += (point, roomType) =>
        {
            resolvedRooms.Add((point.coord, roomType));
            if (defeatAfterFirstRoom && resolvedRooms.Count == 1)
            {
                player.Creature.LoseHpInternal(player.Creature.CurrentHp, default(ValueProp));
            }
        };

        RunDriver.Result result = await driver.RunAsync(maxFloors);
        return new RunCapture(
            runState,
            result,
            runState.VisitedMapCoords.ToArray(),
            decisionSource.Calls.ToArray(),
            resolvedRooms.ToArray(),
            Enum.GetValues<RunRngType>().Select(type => runState.Rng.GetRng(type).Counter).ToArray(),
            player.Creature.CurrentHp,
            player.Gold,
            SnapshotFactory.SnapshotDeck(player).ToArray(),
            SnapshotFactory.SnapshotRelics(player).ToArray(),
            SnapshotFactory.SnapshotPotions(player).ToArray(),
            player.PlayerCombatState is null,
            player.Creature.CombatState is null,
            runState.CurrentRoom,
            runState.IsGameOver);
    }

    private static RunState NewRunState(string seed, MapPointType startingPointType)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = startingPointType;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }

    private static MapPoint FirstTravelPoint(RunState runState) =>
        runState.Map.StartingMapPoint.Children.OrderBy(point => point.coord.col).First();

    private sealed record RunCapture(
        RunState RunState,
        RunDriver.Result Result,
        MapCoord[] VisitedMapCoords,
        string[] Decisions,
        (MapCoord Point, RoomType RoomType)[] ResolvedRooms,
        int[] RunRngCounters,
        int PlayerHp,
        int PlayerGold,
        string[] Deck,
        string[] Relics,
        string?[] Potions,
        bool PlayerCombatStateCleared,
        bool CreatureCombatStateCleared,
        AbstractRoom? CurrentRoom,
        bool IsGameOver);

    private sealed class RecordingDecisionSource : IRunDecisionSource
    {
        public List<string> Calls { get; } = [];

        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options)
        {
            MapPoint[] ordered = options.OrderBy(point => point.coord.col).ToArray();
            MapPoint selected = ordered[0];
            Calls.Add($"map:{string.Join(',', ordered.Select(point => point.coord.ToString()))}->{selected.coord}");
            return Task.FromResult(selected);
        }

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
        {
            Player player = state.Players[0];
            foreach (CardModel card in player.PlayerCombatState!.Hand.Cards)
            {
                if (!card.CanPlay(out _))
                {
                    continue;
                }

                Creature? target = card.TargetType == TargetType.AnyEnemy
                    ? state.HittableEnemies.FirstOrDefault()
                    : null;
                Calls.Add($"combat:play:{card.GetType().Name}:{target?.Monster?.GetType().Name ?? "none"}");
                return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, target));
            }

            Calls.Add("combat:end-turn");
            return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
        }

        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
        {
            RewardDecision decision = RewardDecisionPolicy.Choose(rewards);
            Calls.Add($"reward:{decision.GetType().Name}");
            return Task.FromResult(decision);
        }

        public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player)
        {
            Calls.Add("shop:leave");
            return Task.FromResult<ShopDecision>(new ShopDecision.Leave());
        }

        public Task<RestSiteDecision> ChooseRestSiteActionAsync(
            Player player,
            IReadOnlyList<RestSiteDecision> candidates)
        {
            RestSiteDecision decision = RestSiteDecisionPolicy.ChooseDefault(candidates);
            Calls.Add($"rest:{decision.GetType().Name}");
            return Task.FromResult(decision);
        }

        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options)
        {
            EventOption selected = options[0];
            Calls.Add($"event:{string.Join(',', options.Select(option => option.Key))}->{selected.Key}");
            return Task.FromResult(selected);
        }
    }

    private sealed class ThrowingRestSiteDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options.OrderBy(point => point.coord.col).First());

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<RestSiteDecision> ChooseRestSiteActionAsync(
            Player player,
            IReadOnlyList<RestSiteDecision> candidates) =>
            throw new InvalidOperationException("Rest-site decision failed.");
    }

    private sealed class StubAncientEvent(Action onEntered) : EventModel
    {
        protected override void CalculateVars() => onEntered();

        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [
            new EventOption("DONE", () =>
            {
                Finish();
                return Task.CompletedTask;
            }),
        ];
    }

    private sealed class LethalStartingRoom : AbstractRoom
    {
        public override RoomType RoomType => RoomType.Event;

        public override ModelId? ModelId => null;

        public override Task EnterInternal(RunState? runState)
        {
            Player player = runState!.Players[0];
            player.Creature.LoseHpInternal(player.Creature.MaxHp, default);
            return Task.CompletedTask;
        }

        public override Task Exit(RunState? runState) => Task.CompletedTask;
    }

    private sealed class ThrowingStartingRoom : AbstractRoom
    {
        public override RoomType RoomType => RoomType.Event;

        public override ModelId? ModelId => null;

        public override Task EnterInternal(RunState? runState) =>
            throw new InvalidOperationException("Ancient processing failed.");

        public override Task Exit(RunState? runState) => Task.CompletedTask;
    }

    private sealed record FloorEntry(MapPoint Point, RoomType RoomType);

    private sealed class RecordingRunRecorder : IRunRecorder
    {
        public List<string> Calls { get; } = [];

        public List<FloorEntry> Floors { get; } = [];

        public RunState? BegunRunState { get; private set; }

        public (bool Won, int FloorsVisited, int FinalHp)? EndRunSummary { get; private set; }

        public bool IsFloorActive { get; private set; }

        public bool ThrowOnExit { get; init; }

        public int FloorDetailCount { get; private set; }

        public int CombatCallCount { get; private set; }

        public int CombatEndCount { get; private set; }

        public IReadOnlyDictionary<string, CombatLog> CombatLogs => new Dictionary<string, CombatLog>();

        public void BeginRun(RunState runState)
        {
            BegunRunState = runState;
            Calls.Add("BeginRun");
        }

        public void EnterFloor(MapPoint point, RoomType roomType)
        {
            IsFloorActive = true;
            Floors.Add(new FloorEntry(point, roomType));
            Calls.Add("EnterFloor");
        }

        public void ExitFloor()
        {
            if (ThrowOnExit)
            {
                throw new InvalidOperationException("Recorder exit failed.");
            }
            if (!IsFloorActive)
            {
                throw new InvalidOperationException("ExitFloor requires an active floor.");
            }

            IsFloorActive = false;
            Calls.Add("ExitFloor");
        }

        public void EndRun(
            bool won,
            int floorsVisited,
            int finalHp,
            bool reachedBoss = false,
            bool? survived = null,
            bool truncated = false,
            int actsCleared = 0)
        {
            EndRunSummary = (won, floorsVisited, finalHp);
            Calls.Add("EndRun");
        }

        public void RecordFloorDetail(FloorDetail detail)
        {
            if (!IsFloorActive)
            {
                throw new InvalidOperationException("Floor detail requires an active floor.");
            }

            FloorDetailCount++;
        }

        public string BeginCombat(
            RunState runState,
            RoomType encounterType,
            string encounterName,
            CombatState combatState)
        {
            CombatCallCount++;
            return "test-combat";
        }

        public void RecordTurnStart(CombatState combatState) => CombatCallCount++;

        public void RecordDraw(CardModel card) => CombatCallCount++;

        public void RecordCardPlay(
            CardPlay cardPlay,
            PlayerSnapshot before,
            PlayerSnapshot after,
            IReadOnlyList<EnemySnapshot> enemiesBefore,
            IReadOnlyList<EnemySnapshot> enemiesAfter) => CombatCallCount++;

        public void RecordPotionUse(
            PotionModel potion,
            Creature? target,
            PlayerSnapshot before,
            PlayerSnapshot after,
            IReadOnlyList<EnemySnapshot> enemiesBefore,
            IReadOnlyList<EnemySnapshot> enemiesAfter) => CombatCallCount++;

        public void RecordEnemyAction(
            Creature source,
            string moveId,
            IReadOnlyList<ActionSnapshotSegment> segments) => CombatCallCount++;

        public void RecordEndTurn(
            PlayerSnapshot finalPlayer,
            IReadOnlyList<EnemySnapshot> finalEnemies) => CombatCallCount++;

        public void EndCombat(
            bool victory,
            PlayerSnapshot finalPlayer,
            IReadOnlyList<EnemySnapshot> finalEnemies,
            CombatRewards rewards)
        {
            CombatCallCount++;
            CombatEndCount++;
        }

        public RunManifest BuildManifest() => throw UnexpectedCombatCall();

        private static InvalidOperationException UnexpectedCombatCall() =>
            new("Task 5 must only emit run and floor lifecycle calls.");
    }
}
