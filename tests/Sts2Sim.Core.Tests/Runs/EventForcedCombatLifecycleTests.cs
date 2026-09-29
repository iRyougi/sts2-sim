using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

file sealed class SuspendingForcedCombatEvent : EventModel
{
    public ForcedCombatOutcome? SeenOutcome { get; private set; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("FIGHT", () =>
        {
            RequestForcedCombat(() => (MonsterModel)ModelDb.Monster<LifecycleProbeMonster>().MutableClone());
            SuspendForForcedCombat();
            return Task.CompletedTask;
        }),
    ];

    protected override void AfterForcedCombat(ForcedCombatOutcome outcome)
    {
        SeenOutcome = outcome;
        Finish();
    }
}

file sealed class LifecycleProbeMonster : MonsterModel
{
    public override int MinInitialHp => 1;
    public override int MaxInitialHp => 1;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var wait = new MoveState("WAIT", _ => Task.CompletedTask, new SingleAttackIntent(1));
        wait.FollowUpState = wait;
        return new MonsterMoveStateMachine([wait], wait);
    }
}

file sealed class TimedOutForcedCombatEvent : EventModel
{
    public ForcedCombatOutcome? SeenOutcome { get; private set; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("FIGHT", () =>
        {
            RequestForcedCombat(() => (MonsterModel)ModelDb.Monster<BattleFriendV1>().MutableClone());
            SuspendForForcedCombat();
            return Task.CompletedTask;
        }),
    ];

    protected override void AfterForcedCombat(ForcedCombatOutcome outcome)
    {
        SeenOutcome = outcome;
        Finish();
    }
}

[Collection("ModelDb")]
public sealed class EventForcedCombatLifecycleTests : IDisposable
{
    public EventForcedCombatLifecycleTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
        [
            typeof(SuspendingForcedCombatEvent), typeof(LifecycleProbeMonster),
            typeof(TimedOutForcedCombatEvent), typeof(BattleFriendV1), typeof(BattlewornDummyTimeLimitPower),
            typeof(DenseVegetation), typeof(Wriggler), typeof(Infection), typeof(StrengthPower),
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight),
        ]);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedForcedCombat_ResumesEventWithVictoryOutcome(bool useDriver)
    {
        (RunState runState, Player player, EventRoom room) = await EnterEvent<SuspendingForcedCombatEvent>(
            $"suspended-forced-combat-{useDriver}");

        await DriveEventAsync(useDriver, runState, room);

        var @event = Assert.IsType<SuspendingForcedCombatEvent>(room.Event);
        Assert.Equal(new ForcedCombatOutcome(Victory: true, TimedOut: false), @event.SeenOutcome);
        Assert.True(@event.IsFinished);
        Assert.False(@event.IsAwaitingForcedCombat);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DenseVegetation_LegacyFinishedEventStillDrivesForcedCombat(bool useDriver)
    {
        (RunState runState, Player player, EventRoom room) = await EnterEvent<DenseVegetation>(
            $"dense-vegetation-{useDriver}");
        player.Creature.SetMaxHpInternal(10_000m);
        player.Creature.HealInternal(10_000m);
        await room.Event.ChooseOption(room.Event.CurrentOptions.Single(option => option.Key == "REST"));

        await DriveEventAsync(useDriver, runState, room);

        Assert.True(room.Event.IsFinished);
        Assert.False(room.Event.HasPendingForcedCombat);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
    }

    [Fact]
    public async Task TimedOutForcedCombat_ResumesEventWithTimedOutVictoryOutcome()
    {
        (RunState runState, _, EventRoom room) = await EnterEvent<TimedOutForcedCombatEvent>(
            "timed-out-forced-combat");

        await new RunDriver(runState, new EndTurnDecisionSource()).DriveEventAsync(room);

        var @event = Assert.IsType<TimedOutForcedCombatEvent>(room.Event);
        Assert.Equal(new ForcedCombatOutcome(Victory: true, TimedOut: true), @event.SeenOutcome);
        Assert.True(@event.IsFinished);
    }
    private static async Task<(RunState, Player, EventRoom)> EnterEvent<TEvent>(string seed)
        where TEvent : EventModel
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new EventRoom(() => (EventModel)ModelDb.Event<TEvent>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        return (runState, player, room);
    }

    private static Task DriveEventAsync(bool useDriver, RunState runState, EventRoom room) =>
        useDriver
            ? new RunDriver(runState, new LifecycleDecisionSource()).DriveEventAsync(room)
            : new RunEngine(runState, _ => throw new InvalidOperationException("No map decision is expected.")).DriveEventAsync(room);

    private sealed class EndTurnDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            throw new InvalidOperationException("No map decision is expected.");

        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) => Task.FromResult(options[0]);

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
            Task.FromResult<RewardDecision>(new RewardDecision.Done());
    }

    private sealed class LifecycleDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            throw new InvalidOperationException("No map decision is expected.");

        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
            Task.FromResult(options[0]);

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
        {
            Player player = state.Players[0];
            CardModel? attack = player.PlayerCombatState!.Hand.Cards
                .FirstOrDefault(card => card.Type == CardType.Attack && card.CanPlay(out _));
            return Task.FromResult<CombatDecision>(attack is null
                ? new CombatDecision.EndTurn()
                : new CombatDecision.PlayCard(attack, state.HittableEnemies[0]));
        }

        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
            Task.FromResult(RewardDecisionClassifier.ChooseDefault(rewards));
    }
}
