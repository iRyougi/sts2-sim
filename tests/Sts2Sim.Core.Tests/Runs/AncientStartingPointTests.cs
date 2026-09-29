using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class AncientStartingPointTests : IDisposable
{
    private sealed class StubAncientEvent : EventModel
    {
        private readonly Action _onEntered;

        public StubAncientEvent(Action onEntered)
        {
            _onEntered = onEntered;
        }

        protected override void CalculateVars() => _onEntered();

        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
            new[]
            {
                new EventOption("DONE", () =>
                {
                    Finish();
                    return Task.CompletedTask;
                }),
            };
    }

    private sealed class FirstOptionDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options[0]);

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }

    private sealed class ThrowingAncientEvent : EventModel
    {
        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
            new[]
            {
                new EventOption(
                    "THROW",
                    () => throw new InvalidOperationException("Ancient option failed.")),
            };
    }

    private sealed class ThrowingForcedCombatMonster : MonsterModel
    {
        public override int MinInitialHp => 999;

        public override int MaxInitialHp => 999;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var move = new MoveState(
                "THROW",
                _ => throw new InvalidOperationException("Forced combat failed."));
            move.FollowUpState = move;
            return new MonsterMoveStateMachine(new[] { move }, move);
        }
    }

    private sealed class ForcesThrowingCombatEvent : EventModel
    {
        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
            new[]
            {
                new EventOption("FIGHT", () =>
                {
                    RequestForcedCombat(() =>
                        (MonsterModel)new ThrowingForcedCombatMonster().MutableClone());
                    Finish();
                    return Task.CompletedTask;
                }),
            };
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

    public AncientStartingPointTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void ResolveRoomType_AncientMapsDirectlyToEventWithoutUnknownConsumption()
    {
        RunState runState = CreateRunState("ancient-resolve");
        var point = new MapPoint(0, 0) { PointType = MapPointType.Ancient };
        int unknownRngBefore = runState.Rng.UnknownMapPoint.Counter;

        RoomType roomType = RoomFactory.ResolveRoomType(runState, point);

        Assert.Equal(RoomType.Event, roomType);
        Assert.Equal(unknownRngBefore, runState.Rng.UnknownMapPoint.Counter);
    }

    [Fact]
    public async Task CreateAncientEventRoom_EntersRealNeowWithoutUnknownConsumption()
    {
        RunState runState = CreateRunState("ancient-factory");
        int unknownRngBefore = runState.Rng.UnknownMapPoint.Counter;

        var room = Assert.IsType<EventRoom>(RoomFactory.CreateAncientEventRoom(runState));
        await room.Enter(runState);

        Assert.IsType<Neow>(room.Event);
        Assert.Equal(unknownRngBefore, runState.Rng.UnknownMapPoint.Counter);
    }

    [Fact]
    public async Task RunEngine_EntersAndCompletesInjectedAncientBeforeFloorLoop()
    {
        RunState runState = CreateRunState("ancient-engine");
        bool entered = false;
        var engine = new RunEngine(
            runState,
            points => points[0],
            createAncientEventRoom: _ =>
                new EventRoom(() =>
                    (EventModel)new StubAncientEvent(() => entered = true).MutableClone()));

        RunEngine.Result result = await engine.RunAsync(maxFloors: 0);

        Assert.True(entered);
        Assert.Equal(0, result.FloorsVisited);
        Assert.Null(runState.CurrentRoom);
    }

    [Fact]
    public async Task RunDriver_EntersAndCompletesInjectedAncientBeforeFloorLoop()
    {
        RunState runState = CreateRunState("ancient-driver");
        bool entered = false;
        var driver = new RunDriver(
            runState,
            new FirstOptionDecisionSource(),
            createAncientEventRoom: _ =>
                new EventRoom(() =>
                    (EventModel)new StubAncientEvent(() => entered = true).MutableClone()));

        RunDriver.Result result = await driver.RunAsync(maxFloors: 0);

        Assert.True(entered);
        Assert.Equal(0, result.FloorsVisited);
        Assert.Null(runState.CurrentRoom);
    }

    [Fact]
    public async Task RunEngine_DefaultFactoryCompletesRealNeowBeforeFloorLoop()
    {
        RunState runState = CreateRunState("ancient-engine-default");
        Player player = runState.Players[0];
        int relicsBefore = player.Relics.Count;
        var engine = new RunEngine(runState, points => points[0]);

        RunEngine.Result result = await engine.RunAsync(maxFloors: 0);

        RelicModel relic = Assert.Single(player.Relics.Skip(relicsBefore));
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.Same(player, relic.Owner);
        Assert.Equal(0, result.FloorsVisited);
        Assert.Null(runState.CurrentRoom);
    }

    [Fact]
    public async Task RunDriver_DefaultFactoryCompletesRealNeowBeforeFloorLoop()
    {
        RunState runState = CreateRunState("ancient-driver-default");
        Player player = runState.Players[0];
        int relicsBefore = player.Relics.Count;
        var driver = new RunDriver(runState, new FirstOptionDecisionSource());

        RunDriver.Result result = await driver.RunAsync(maxFloors: 0);

        RelicModel relic = Assert.Single(player.Relics.Skip(relicsBefore));
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.Same(player, relic.Owner);
        Assert.Equal(0, result.FloorsVisited);
        Assert.Null(runState.CurrentRoom);
    }

    [Fact]
    public async Task RunEngine_NonAncientStartDoesNotInvokeAncientFactory()
    {
        RunState runState = CreateRunState("non-ancient-engine");
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        bool invoked = false;
        var engine = new RunEngine(
            runState,
            points => points[0],
            createAncientEventRoom: _ =>
            {
                invoked = true;
                throw new InvalidOperationException("Ancient factory should not be called.");
            });

        RunEngine.Result result = await engine.RunAsync(maxFloors: 0);

        Assert.False(invoked);
        Assert.Equal(0, result.FloorsVisited);
    }

    [Fact]
    public async Task RunDriver_NonAncientStartDoesNotInvokeAncientFactory()
    {
        RunState runState = CreateRunState("non-ancient-driver");
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        bool invoked = false;
        var driver = new RunDriver(
            runState,
            new FirstOptionDecisionSource(),
            createAncientEventRoom: _ =>
            {
                invoked = true;
                throw new InvalidOperationException("Ancient factory should not be called.");
            });

        RunDriver.Result result = await driver.RunAsync(maxFloors: 0);

        Assert.False(invoked);
        Assert.Equal(0, result.FloorsVisited);
    }

    [Fact]
    public async Task RunEngine_CleansUpAncientRoomWhenEventOptionThrows()
    {
        RunState runState = CreateRunState("throwing-ancient-engine");
        var engine = new RunEngine(
            runState,
            points => points[0],
            createAncientEventRoom: _ =>
                new EventRoom(() =>
                    (EventModel)new ThrowingAncientEvent().MutableClone()));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunAsync(maxFloors: 0));

        Assert.Equal("Ancient option failed.", exception.Message);
        Assert.Null(runState.CurrentRoom);
    }

    [Fact]
    public async Task RunDriver_CleansUpAncientRoomWhenEventOptionThrows()
    {
        RunState runState = CreateRunState("throwing-ancient-driver");
        var driver = new RunDriver(
            runState,
            new FirstOptionDecisionSource(),
            createAncientEventRoom: _ =>
                new EventRoom(() =>
                    (EventModel)new ThrowingAncientEvent().MutableClone()));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 0));

        Assert.Equal("Ancient option failed.", exception.Message);
        Assert.Null(runState.CurrentRoom);
    }

    [Fact]
    public async Task RunEngine_TearsDownForcedCombatWhenMonsterMoveThrows()
    {
        RunState runState = CreateRunState("throwing-forced-combat-engine");
        Player player = runState.Players[0];
        var engine = new RunEngine(
            runState,
            points => points[0],
            createAncientEventRoom: _ =>
                new EventRoom(() =>
                    (EventModel)new ForcesThrowingCombatEvent().MutableClone()));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunAsync(maxFloors: 0));

        Assert.Equal("Forced combat failed.", exception.Message);
        Assert.Null(runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
    }

    [Fact]
    public async Task RunDriver_TearsDownForcedCombatWhenMonsterMoveThrows()
    {
        RunState runState = CreateRunState("throwing-forced-combat-driver");
        Player player = runState.Players[0];
        var driver = new RunDriver(
            runState,
            new FirstOptionDecisionSource(),
            createAncientEventRoom: _ =>
                new EventRoom(() =>
                    (EventModel)new ForcesThrowingCombatEvent().MutableClone()));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 0));

        Assert.Equal("Forced combat failed.", exception.Message);
        Assert.Null(runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
    }

    [Fact]
    public async Task RunEngine_StopsBeforeTravelWhenAncientRoomEndsTheRun()
    {
        RunState runState = CreateRunState("lethal-ancient-engine");
        var engine = new RunEngine(
            runState,
            _ => throw new InvalidOperationException("Travel should not be requested after game over."),
            createAncientEventRoom: _ => new LethalStartingRoom());

        RunEngine.Result result = await engine.RunAsync(maxFloors: 1);

        Assert.True(runState.IsGameOver);
        Assert.False(result.Won);
        Assert.Equal(0, result.FloorsVisited);
        Assert.Null(runState.CurrentRoom);
    }

    [Fact]
    public async Task RunDriver_StopsBeforeTravelWhenAncientRoomEndsTheRun()
    {
        RunState runState = CreateRunState("lethal-ancient-driver");
        var driver = new RunDriver(
            runState,
            new FirstOptionDecisionSource(),
            createAncientEventRoom: _ => new LethalStartingRoom());

        RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

        Assert.True(runState.IsGameOver);
        Assert.False(result.Won);
        Assert.Equal(0, result.FloorsVisited);
        Assert.Null(runState.CurrentRoom);
    }

    private static RunState CreateRunState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }
}
