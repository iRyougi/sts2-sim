using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Runs;

public sealed class RunEngineRecorderTests : IDisposable
{
    public RunEngineRecorderTests()
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
        Assert.Equal(omitted.MapDecisions, explicitNull.MapDecisions);
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
        RunCapture? capture = null;
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            capture = await RunOnceAsync(
                recorder,
                passRecorderArgument: true,
                maxFloors: 0,
                startingPointType: MapPointType.Ancient,
                createAncientEventRoom: _ => new ThrowingStartingRoom());
        });

        Assert.Equal("Ancient processing failed.", exception.Message);
        Assert.DoesNotContain("ExitFloor", recorder.Calls);
        Assert.Null(capture);
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
        RunCapture capture = await RunOnceAsync(recorder, passRecorderArgument: true, defeatAfterFirstRoom: true);

        Assert.False(capture.Result.Won);
        Assert.True(capture.IsGameOver);
        Assert.Equal(1, capture.Result.FloorsVisited);
        Assert.Single(recorder.Floors);
        Assert.Equal(["BeginRun", "EnterFloor", "ExitFloor", "EndRun"], recorder.Calls);
        Assert.Equal((false, 1, 0), recorder.EndRunSummary);
        Assert.True(recorder.CombatCallCount > 0);
        Assert.Equal(1, recorder.CombatEndCount);
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
        var runState = new RunState("run-engine-recorder", new Overgrowth());
        runState.Map.StartingMapPoint.PointType = startingPointType;
        if (forceFirstTravelRoomMonster)
        {
            runState.Map.StartingMapPoint.Children.OrderBy(point => point.coord.col).First().PointType = MapPointType.Monster;
        }

        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var resolvedRooms = new List<(MapCoord Point, RoomType RoomType)>();
        var mapDecisions = new List<string>();
        MapPoint ChoosePoint(IReadOnlyList<MapPoint> points)
        {
            MapPoint[] ordered = points.OrderBy(point => point.coord.col).ToArray();
            MapPoint selected = ordered[0];
            mapDecisions.Add($"{string.Join(',', ordered.Select(point => point.coord.ToString()))}->{selected.coord}");
            return selected;
        }

        RunEngine engine = passRecorderArgument
            ? new RunEngine(runState, ChoosePoint, createAncientEventRoom, recorder)
            : new RunEngine(runState, ChoosePoint, createAncientEventRoom);
        engine.OnRoomResolved += (point, roomType) =>
        {
            resolvedRooms.Add((point.coord, roomType));
            if (defeatAfterFirstRoom && resolvedRooms.Count == 1)
            {
                player.Creature.LoseHpInternal(player.Creature.CurrentHp, default(ValueProp));
            }
        };

        RunEngine.Result result = await engine.RunAsync(maxFloors);
        return new RunCapture(
            runState,
            result,
            runState.VisitedMapCoords.ToArray(),
            mapDecisions.ToArray(),
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

    private sealed record RunCapture(
        RunState RunState,
        RunEngine.Result Result,
        MapCoord[] VisitedMapCoords,
        string[] MapDecisions,
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

        public RunManifest BuildManifest() => throw UnexpectedCombatOrDetailCall();

        private static InvalidOperationException UnexpectedCombatOrDetailCall() =>
            new("Task 4 must only emit run and floor lifecycle calls.");
    }
}
