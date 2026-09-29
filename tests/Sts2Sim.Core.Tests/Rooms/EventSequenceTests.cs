using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public sealed class EventSequenceTests : IDisposable
{
    private static readonly Type[] EligibleAct1Events =
    [
        .. Act1EventPool.All, typeof(BrainLeech), typeof(RoomFullOfCheese), typeof(SelfHelpBook),
        typeof(SlipperyBridge), typeof(TeaMaster), typeof(TheFutureOfPotions), typeof(TheLegendsWereTrue), typeof(ThisOrThat),
    ];
    // Gate/fallback tests control their queue independently of seeded generation.
    private static readonly Type[] SequenceFixture =
    {
        typeof(SapphireSeed),
        typeof(JungleMazeAdventure),
        typeof(LuminousChoir),
        typeof(WhisperingHollow),
        typeof(AromaOfChaos),
        typeof(WoodCarvings),
        typeof(ThisOrThat),
        typeof(SlipperyBridge),
        typeof(ByrdonisNest),
        typeof(TabletOfTruth),
        typeof(Wellspring),
        typeof(MorphicGrove),
        typeof(SunkenStatue),
        typeof(TeaMaster),
        typeof(SelfHelpBook),
        typeof(RoomFullOfCheese),
        typeof(DenseVegetation),
        typeof(TheFutureOfPotions),
        typeof(BrainLeech),
        typeof(TheLegendsWereTrue),
        typeof(UnrestSite),
    };
    private const string SequenceSeed = "fixed-event-sequence";
    private const string FallbackSeed = "fixed-event-fallback";
    public EventSequenceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(6, typeof(PunchOff), typeof(ThisOrThat))]
    [InlineData(7, typeof(PunchOff), typeof(PunchOff))]
    [InlineData(7, typeof(SlipperyBridge), typeof(ThisOrThat))]
    [InlineData(8, typeof(SlipperyBridge), typeof(SlipperyBridge))]
    public async Task MapEventEligibility_UsesHistoryBeforeDestinationIsAppended(int floor, Type candidate, Type expected)
    {
        // Native RunManager.CreateRoom precedes AppendToMapPointHistory, so PunchOff's
        // TotalFloor >= 6 gate first admits the event at destination floor 7.
        RunState run = NewRunState("issue-59-history-boundary");
        for (int row = 0; row < floor; row++)
            run.AddVisitedMapCoord(new MapCoord(0, row));
        typeof(RunState).GetField("_eventSequence",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(run, new List<Type> { candidate, typeof(ThisOrThat) });

        var room = Assert.IsType<EventRoom>(RoomFactory.CreateRoom(run, RoomType.Event));
        Assert.Equal(floor, run.TotalFloor);
        await EnterAndExit(run, room);
        Assert.Equal(expected, room.Event.GetType());
        Assert.Equal(floor, run.TotalFloor);
    }

    [Theory]
    [InlineData("plan06g-sequence-a")]
    [InlineData("plan06g-sequence-b")]
    [InlineData("plan06g-sequence-c")]
    [InlineData("plan06g-sequence-d")]
    public async Task PullNextEvent_MultipleSeedsProduceCompletePermutationWithoutPullRng(string seed)
    {
        RunState runState = NewRunState(seed);
        await AllowEveryEvent(runState);
        Dictionary<RunRngType, int> countersBefore = Enum.GetValues<RunRngType>()
            .ToDictionary(type => type, type => runState.Rng.GetRng(type).Counter);

        Type[] actual = Enumerable.Range(0, EligibleAct1Events.Length)
            .Select(_ => runState.PullNextEvent())
            .ToArray();

        Assert.Equal(
            EligibleAct1Events.OrderBy(type => type.FullName),
            actual.OrderBy(type => type.FullName));
        Assert.Equal(21, actual.Distinct().Count());
        foreach ((RunRngType type, int counter) in countersBefore)
        {
            Assert.Equal(counter, runState.Rng.GetRng(type).Counter);
        }
    }
    [Fact]
    public async Task Archived8F9CPYQ6QYEN_FirstHiveEventIsTheFutureOfPotions()
    {
        // Archive F18 is Pael and F21 is TheFutureOfPotions. The potion pair
        // supplies that event's recorded eligibility without replaying intervening combat.
        var run = new RunState("8F9CPYQ6QYEN", ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        run.AdvanceToNextAct();
        player.AddPotionInternal((PotionModel)ModelDb.Potion<FoulPotion>().MutableClone());
        player.AddPotionInternal((PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone());
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var ancient = Assert.IsType<EventRoom>(RoomFactory.CreateAncientEventRoom(run));
        run.PushRoom(ancient);
        try
        {
            await ancient.Enter(run);
            Assert.IsType<Pael>(ancient.Event);
        }
        finally
        {
            await ancient.Exit(run);
            run.PopCurrentRoom();
        }

        Assert.Equal(typeof(TheFutureOfPotions), run.PullNextEvent());
    }
    [Fact]
    public async Task RoomFactory_EventPath_UsesTheFixedUpFrontSequenceWithoutPullRng()
    {
        RunState runState = NewRunState(SequenceSeed);
        int unknownCounter = runState.Rng.UnknownMapPoint.Counter;
        int upFrontCounter = runState.Rng.UpFront.Counter;

        var firstRoom = Assert.IsType<EventRoom>(RoomFactory.CreateRoom(runState, RoomType.Event));
        var secondRoom = Assert.IsType<EventRoom>(RoomFactory.CreateRoom(runState, RoomType.Event));
        await EnterAndExit(runState, firstRoom);
        await EnterAndExit(runState, secondRoom);

        Assert.IsType<SapphireSeed>(firstRoom.Event);
        Assert.IsType<JungleMazeAdventure>(secondRoom.Event);
        Assert.Equal(2, runState.EventsVisited);
        Assert.Equal(2, runState.VisitedEventIds.Count);
        Assert.Equal(unknownCounter, runState.Rng.UnknownMapPoint.Counter);
        Assert.Equal(upFrontCounter, runState.Rng.UpFront.Counter);
    }

    [Fact]
    public async Task PullNextEvent_FixedSequenceIsACompletePermutationAndRecordsEveryId()
    {
        RunState runState = NewRunState(SequenceSeed);
        await AllowEveryEvent(runState);
        int upFrontCounter = runState.Rng.UpFront.Counter;
        int unknownCounter = runState.Rng.UnknownMapPoint.Counter;

        Type[] actual = Enumerable.Range(0, EligibleAct1Events.Length)
            .Select(_ => runState.PullNextEvent())
            .ToArray();

        Assert.Equal(SequenceFixture, actual);
        Assert.Equal(
            EligibleAct1Events.OrderBy(type => type.FullName),
            actual.OrderBy(type => type.FullName));
        Assert.Equal(21, actual.Distinct().Count());
        Assert.Equal(
            actual.Select(type => ModelDb.Get(type).Id).OrderBy(id => id),
            runState.VisitedEventIds.OrderBy(id => id));
        Assert.Equal(21, runState.EventsVisited);
        Assert.Equal(upFrontCounter, runState.Rng.UpFront.Counter);
        Assert.Equal(unknownCounter, runState.Rng.UnknownMapPoint.Counter);
    }

    [Fact]
    public void PullNextEvent_PersistsGateSkipAndDoesNotReprobeItOnTheNextPull()
    {
        RunState runState = NewRunState(SequenceSeed);
        ModelId luminousChoirId = ModelDb.Event<LuminousChoir>().Id;

        Assert.Equal(typeof(SapphireSeed), runState.PullNextEvent());
        Assert.Equal(typeof(JungleMazeAdventure), runState.PullNextEvent());
        Assert.Equal(typeof(WhisperingHollow), runState.PullNextEvent());
        Assert.Equal(4, runState.EventsVisited);
        Assert.DoesNotContain(luminousChoirId, runState.VisitedEventIds);

        runState.Players[0].Gold = 200;
        Assert.Equal(typeof(AromaOfChaos), runState.PullNextEvent());
        Assert.Equal(5, runState.EventsVisited);
        Assert.DoesNotContain(luminousChoirId, runState.VisitedEventIds);
    }

    [Fact]
    public async Task PullNextEvent_AfterFullVisitedCycleFallsBackToVisitedDisallowedCandidate()
    {
        RunState runState = NewRunState(FallbackSeed);
        await AllowEveryEvent(runState);
        Type[] firstCycle = Enumerable.Range(0, EligibleAct1Events.Length)
            .Select(_ => runState.PullNextEvent())
            .ToArray();
        Assert.Equal(typeof(LuminousChoir), firstCycle[0]);
        Assert.Equal(21, firstCycle.Distinct().Count());

        runState.Players[0].Gold = 0;
        int upFrontCounter = runState.Rng.UpFront.Counter;
        int unknownCounter = runState.Rng.UnknownMapPoint.Counter;

        Type fallback = runState.PullNextEvent();

        Assert.Equal(typeof(LuminousChoir), fallback);
        Assert.Equal(43, runState.EventsVisited);
        Assert.Equal(21, runState.VisitedEventIds.Count);
        Assert.Equal(upFrontCounter, runState.Rng.UpFront.Counter);
        Assert.Equal(unknownCounter, runState.Rng.UnknownMapPoint.Counter);
    }

    [Fact]
    public async Task PullNextEvent_FallbackRecordsPreviouslyUnvisitedDisallowedCandidate()
    {
        RunState runState = NewRunState(FallbackSeed);
        await AllowEveryEvent(runState);
        runState.Players[0].Gold = 100;
        ModelId luminousChoirId = ModelDb.Event<LuminousChoir>().Id;

        // 100 gold rejects both LuminousChoir and TeaMaster from the 21 eligible candidates.
        Type[] acceptedBeforeFallback = Enumerable.Range(0, EligibleAct1Events.Length - 2)
            .Select(_ => runState.PullNextEvent())
            .ToArray();
        Assert.DoesNotContain(typeof(LuminousChoir), acceptedBeforeFallback);
        Assert.DoesNotContain(luminousChoirId, runState.VisitedEventIds);
        Assert.Equal(19, runState.VisitedEventIds.Count);
        Assert.Equal(21, runState.EventsVisited);
        int upFrontCounter = runState.Rng.UpFront.Counter;
        int unknownCounter = runState.Rng.UnknownMapPoint.Counter;

        Type fallback = runState.PullNextEvent();

        Assert.Equal(typeof(LuminousChoir), fallback);
        Assert.Contains(luminousChoirId, runState.VisitedEventIds);
        Assert.Equal(20, runState.VisitedEventIds.Count);
        Assert.Equal(43, runState.EventsVisited);
        Assert.Equal(upFrontCounter, runState.Rng.UpFront.Counter);
        Assert.Equal(unknownCounter, runState.Rng.UnknownMapPoint.Counter);
    }

    private static async Task AllowEveryEvent(RunState runState)
    {
        Player player = runState.Players[0];
        player.Gold = 200;
        player.AddPotionInternal(ModelDb.Potion<FoulPotion>());
        player.AddPotionInternal(ModelDb.Potion<FoulPotion>());
        for (int floor = 0; floor < 7; floor++) runState.AddVisitedMapCoord(new MapCoord(0, floor));
        await CreatureCmd.LoseHp(
            runState,
            player.Creature,
            player.Creature.CurrentHp - 10m,
            ValueProp.Unblockable);
    }

    private static async Task EnterAndExit(RunState runState, EventRoom room)
    {
        runState.PushRoom(room);
        try
        {
            await room.Enter(runState);
            await room.Exit(runState);
        }
        finally
        {
            Assert.Same(room, runState.PopCurrentRoom());
        }
    }

    private static RunState NewRunState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        _ = runState.CurrentAncientEventType; // Finish real lazy generation before RNG snapshots.
        if (seed == SequenceSeed || seed == FallbackSeed)
        {
            List<Type> fixture = seed == SequenceSeed
                ? SequenceFixture.ToList()
                : new[] { typeof(LuminousChoir) }
                    .Concat(SequenceFixture.Where(type => type != typeof(LuminousChoir))).ToList();
            typeof(RunState).GetField("_eventSequence",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(runState, fixture);
        }
        return runState;
    }
}
