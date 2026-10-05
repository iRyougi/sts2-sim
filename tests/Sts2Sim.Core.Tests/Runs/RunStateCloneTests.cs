using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs.Transplant;
using Sts2Sim.Core.Runs;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Sts2Sim.Core.Tests.Runs;

public sealed partial class RunStateCloneTests : IDisposable
{
    public RunStateCloneTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("clone-floor-2", 2)]
    [InlineData("clone-floor-6", 6)]
    [InlineData("clone-next-act", 17)]
    public async Task CloneExact_ForksKeepFullRngStateAndIndependentContinuations(string seed, int floorsBeforeClone)
    {
        foreach (bool keyed in new[] { false, true })
        {
            RunState source = keyed
                ? RunState.CreateKeyedForLabels(seed, [new Overgrowth(), new Hive(), new Glory()], 10)
                : new RunState(seed, [new Overgrowth(), new Hive(), new Glory()], 10);
            Player sourcePlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), source);
            source.AddPlayer(sourcePlayer);
            sourcePlayer.Creature.SetMaxHpInternal(10_000);
            sourcePlayer.Creature.HealInternal(10_000);
            await new RunDriver(source, new FirstChoiceDecisionSource()).RunAsync(floorsBeforeClone);
            Assert.True(source.TotalFloor >= floorsBeforeClone);
            if (floorsBeforeClone == 17) Assert.True(source.CurrentActIndex >= 1);

            RunState child = source.CloneExact();
            RunState sibling = source.CloneExact();
            AssertExactWorldSnapshot(source, child);
            AssertExactWorldSnapshot(source, sibling);
            Assert.NotSame(source.Map, child.Map);
            Assert.NotSame(child.Map, sibling.Map);
            Assert.NotSame(source.Players[0], child.Players[0]);

            var sourceSteps = new List<string>();
            var childSteps = new List<string>();
            var siblingSteps = new List<string>();
            RunDriver.Result sourceResult = await new RunDriver(source,
                new RunCloneRoomFixture.TraceSource(source, sourceSteps, null)).ContinueAsync();
            RunDriver.Result childResult = await new RunDriver(child,
                new RunCloneRoomFixture.TraceSource(child, childSteps, null)).ContinueAsync();
            RunDriver.Result siblingResult = await new RunDriver(sibling,
                new RunCloneRoomFixture.TraceSource(sibling, siblingSteps, null)).ContinueAsync();
            Assert.True(sourceResult.Outcome is RunOutcome.Victory or RunOutcome.PlayerDefeated);
            Assert.Equal(sourceResult, childResult);
            Assert.Equal(sourceResult, siblingResult);
            Assert.Equal(sourceSteps.Count, childSteps.Count);
            Assert.Equal(sourceSteps.Count, siblingSteps.Count);
            for (int step = 0; step < sourceSteps.Count; step++)
            {
                Assert.Equal(sourceSteps[step], childSteps[step]);
                Assert.Equal(sourceSteps[step], siblingSteps[step]);
            }
            AssertExactWorldSnapshot(source, child);
            AssertExactWorldSnapshot(source, sibling);

            int sourceGold = sourcePlayer.Gold;
            int siblingGold = sibling.Players[0].Gold;
            child.Players[0].Gold++;
            Assert.Equal(sourceGold, sourcePlayer.Gold);
            Assert.Equal(siblingGold, sibling.Players[0].Gold);
            int sourceDeckCount = sourcePlayer.Deck.Cards.Count;
            int siblingDeckCount = sibling.Players[0].Deck.Cards.Count;
            var added = (CardModel)ModelDb.Card<StrikeSilent>().MutableClone();
            added.AssignOwner(child.Players[0]);
            child.Players[0].Deck.AddInternal(added);
            Assert.Equal(sourceDeckCount, sourcePlayer.Deck.Cards.Count);
            Assert.Equal(siblingDeckCount, sibling.Players[0].Deck.Cards.Count);
            sourcePlayer.Gold += 2;
            Assert.Equal(sourceGold + 1, child.Players[0].Gold);
            Assert.Equal(siblingGold, sibling.Players[0].Gold);
            AssertExactRngSnapshots(source, child);
            AssertExactRngSnapshots(source, sibling);
        }
    }

    private static void AssertExactWorldSnapshot(RunState expected, RunState actual)
    {
        Assert.Equal(expected.CurrentActIndex, actual.CurrentActIndex);
        Assert.Equal(expected.TotalFloor, actual.TotalFloor);
        Assert.Equal(expected.CurrentMapCoord, actual.CurrentMapCoord);
        Assert.Equal(expected.VisitedMapCoords, actual.VisitedMapCoords);
        Assert.Equal(expected.Players[0].Creature.CurrentHp, actual.Players[0].Creature.CurrentHp);
        Assert.Equal(expected.Players[0].Creature.MaxHp, actual.Players[0].Creature.MaxHp);
        Assert.Equal(expected.Players[0].Gold, actual.Players[0].Gold);
        Assert.Equal(expected.Players[0].Deck.Cards.Select(card => (card.Id, card.IsUpgraded)),
            actual.Players[0].Deck.Cards.Select(card => (card.Id, card.IsUpgraded)));
        Assert.Equal(expected.Players[0].Relics.Select(relic => (relic.Id, relic.StackCount)),
            actual.Players[0].Relics.Select(relic => (relic.Id, relic.StackCount)));
        Assert.Equal(expected.Map.GetAllMapPoints().Select(point => (point.coord, point.PointType,
                Parents: string.Join(";", point.parents.Select(parent => parent.coord)),
                Children: string.Join(";", point.Children.Select(child => child.coord)))),
            actual.Map.GetAllMapPoints().Select(point => (point.coord, point.PointType,
                Parents: string.Join(";", point.parents.Select(parent => parent.coord)),
                Children: string.Join(";", point.Children.Select(child => child.coord)))));
        AssertExactRngSnapshots(expected, actual);
    }

    private static void AssertExactRngSnapshots(RunState expected, RunState actual)
    {
        Assert.Equal(expected.Rng.StringSeed, actual.Rng.StringSeed);
        Assert.Equal(expected.Rng.Seed, actual.Rng.Seed);
        Assert.Equal(expected.Rng.UsesSemanticKeys, actual.Rng.UsesSemanticKeys);
        Assert.Equal(RngStates(expected.Rng), RngStates(actual.Rng));
        Assert.Equal(expected.Rng.GetKeyedDraws(), actual.Rng.GetKeyedDraws());
        Assert.Equal(expected.Players[0].PlayerRng.Seed, actual.Players[0].PlayerRng.Seed);
        Assert.Equal(RngStates(expected.Players[0].PlayerRng), RngStates(actual.Players[0].PlayerRng));
    }
}


// Replaces the existing five-row future Theory; the final test input uses one model registration fixture.
public sealed partial class RunStateCloneTests
{
    [Theory]
    [InlineData("no-leak")]
    [InlineData("elite-bag-after-3")]
    [InlineData("elite-bag-after-1")]
    [InlineData("weak-to-regular")]
    [InlineData("events")]
    public async Task CloneReseeded_EqualVisibleHistoryAndNewSeedIgnoreOldHiddenFuture(string scenario)
    {
        var source = new RunState("imagination-" + scenario, [new Overgrowth(), new Hive(), new Glory()], 10);
        source.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), source));
        EncounterDefinition? lastElite = null;
        Type? visitedEvent = null;
        switch (scenario)
        {
            case "no-leak":
                source.PullNextEncounter(RoomType.Monster);
                lastElite = source.PullNextEncounter(RoomType.Elite);
                break;
            case "elite-bag-after-3":
            case "elite-bag-after-1":
                int visits = scenario == "elite-bag-after-3" ? 3 : 1;
                for (int visit = 0; visit < visits; visit++) lastElite = source.PullNextEncounter(RoomType.Elite);
                break;
            case "weak-to-regular":
                for (int visit = 0; visit < source.Act.NumberOfWeakEncounters; visit++)
                    source.PullNextEncounter(RoomType.Monster);
                break;
            case "events":
                visitedEvent = source.PullNextEvent();
                break;
        }

        RunState hiddenVariant = source.CloneExact();
        ChangeOnlyOldHiddenFuture(hiddenVariant);
        const ulong newSeed = 0x1001UL;
        RunState first = source.CloneReseeded(newSeed);
        // A collision with the old root seed must not choose an alternative new world.
        if (scenario == "no-leak")
            typeof(RunState).GetField("<Rng>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(hiddenVariant, first.Rng.CloneExact());

        RunState second = hiddenVariant.CloneReseeded(newSeed);
        Assert.Equal(first.Rng.Seed, second.Rng.Seed);
        Assert.Equal(first.Players[0].PlayerRng.Seed, second.Players[0].PlayerRng.Seed);
        Assert.Equal(source.Players[0].Creature.CurrentHp, first.Players[0].Creature.CurrentHp);
        Assert.Equal(source.Players[0].Gold, first.Players[0].Gold);
        Assert.Equal(source.Players[0].Deck.Cards.Select(card => (card.Id, card.IsUpgraded)),
            first.Players[0].Deck.Cards.Select(card => (card.Id, card.IsUpgraded)));
        Assert.Equal(source.Map.GetAllMapPoints().Select(point => (point.coord, point.PointType)),
            first.Map.GetAllMapPoints().Select(point => (point.coord, point.PointType)));
        Assert.Equal(source.CurrentActBossEncounter, first.CurrentActBossEncounter);
        Assert.Equal(RngStates(first.Rng), RngStates(second.Rng));
        Assert.Equal(RngStates(first.Players[0].PlayerRng), RngStates(second.Players[0].PlayerRng));

        switch (scenario)
        {
            case "no-leak":
                for (int draw = 0; draw < 8; draw++)
                    Assert.Equal(first.PullNextEncounter(RoomType.Monster), second.PullNextEncounter(RoomType.Monster));
                Assert.Equal(RngStates(first.Rng), RngStates(second.Rng));
                RunCloneRelicBagCounterfactualFixture.Verify(source.SharedRelicGrabBag!);
                RunCloneRelicBagCounterfactualFixture.Verify(source.Players[0].RelicGrabBag!);
                await RunCloneCurrentActHistoryFixture.VerifyAsync();
                break;
            case "elite-bag-after-3":
            case "elite-bag-after-1":
                EncounterDefinition next = first.PullNextEncounter(RoomType.Elite);
                Assert.Equal(next, second.PullNextEncounter(RoomType.Elite));
                Assert.NotEqual(lastElite, next);
                Assert.Contains(next, source.Act.EliteEncounterCandidates);
                break;
            case "weak-to-regular":
                for (int draw = 0; draw < 4; draw++)
                {
                    EncounterDefinition regular = first.PullNextEncounter(RoomType.Monster);
                    Assert.Equal(regular, second.PullNextEncounter(RoomType.Monster));
                    Assert.False(regular.IsWeak);
                    Assert.Contains(regular, source.Act.MonsterEncounterCandidates);
                }
                break;
            case "events":
                List<Type> firstSequence = PrivateList<Type>(first, "_eventSequence");
                List<Type> secondSequence = PrivateList<Type>(second, "_eventSequence");
                Assert.Equal(firstSequence, secondSequence);
                Assert.DoesNotContain(visitedEvent!, firstSequence);
                Assert.Equal(source.Act.EffectiveEventPool.Except([visitedEvent!]).OrderBy(type => type.FullName),
                    firstSequence.OrderBy(type => type.FullName));
                int remainingEvents = firstSequence.Count;
                for (int draw = 0; draw < remainingEvents; draw++)
                    Assert.Equal(first.PullNextEvent(), second.PullNextEvent());
                break;
        }
    }

    private static void ChangeOnlyOldHiddenFuture(RunState run)
    {
        foreach (PropertyInfo property in run.Rng.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            if (property.PropertyType == typeof(Rng)) ((Rng)property.GetValue(run.Rng)!).NextInt(97);
        foreach (PropertyInfo property in run.Players[0].PlayerRng.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            if (property.PropertyType == typeof(Rng)) ((Rng)property.GetValue(run.Players[0].PlayerRng)!).NextInt(97);
        ReverseUnvisited<EncounterDefinition>(run, "_normalEncounterSequence", "_normalEncountersVisited");
        ReverseUnvisited<EncounterDefinition>(run, "_eliteEncounterSequence", "_eliteEncountersVisited");
        ReverseUnvisited<Type>(run, "_eventSequence", "_eventsVisited");
    }

    private static void ReverseUnvisited<T>(RunState run, string sequenceField, string cursorField)
    {
        List<T> sequence = PrivateList<T>(run, sequenceField);
        int cursor = (int)typeof(RunState).GetField(cursorField, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!;
        sequence.Reverse(cursor, sequence.Count - cursor);
    }

    private static List<T> PrivateList<T>(RunState run, string field) =>
        (List<T>)typeof(RunState).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!;

    private static KeyValuePair<string, Sts2Sim.Core.Saves.SerializableRng>[] RngStates(object streams) =>
        streams.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(Rng)).OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => new KeyValuePair<string, Sts2Sim.Core.Saves.SerializableRng>(property.Name,
                ((Rng)property.GetValue(streams)!).ToSerializable())).ToArray();
}


public sealed partial class RunStateCloneTests
{
    [Theory]
    [InlineData("tablet-decipher")]
    [InlineData("hidden-preroll")]
    [InlineData("reward-visible")]
    [InlineData("registry-coverage")]
    public async Task CloneReseeded_InRoomUsesOnlyVisibleConditions(string scenario)
    {
        if (scenario == "tablet-decipher")
        {
            var source = NewVisibleRun("in-room-" + scenario);
            var room = await EnterEventAsync<TabletOfTruth>(source);
            RunState variant = source.CloneExact();
            ChangeOnlyOldHiddenFuture(variant);
            ((Rng)typeof(EventModel).GetField("_rng", PrivateFields)!.GetValue(variant.CurrentRoom is EventRoom e ? e.Event : null)!).NextInt(97);
            var before = RunCloneGraphSnapshot.Capture(source);
            RunState first = source.CloneReseeded(97);
            RunState second = variant.CloneReseeded(97);
            var firstEvent = Assert.IsType<EventRoom>(first.CurrentRoom).Event;
            var secondEvent = Assert.IsType<EventRoom>(second.CurrentRoom).Event;
            Assert.Equal(room.Event.CurrentOptions.Select(o => (o.Key, o.IsLocked)), firstEvent.CurrentOptions.Select(o => (o.Key, o.IsLocked)));
            await firstEvent.ChooseOption(firstEvent.CurrentOptions[0]);
            await secondEvent.ChooseOption(secondEvent.CurrentOptions[0]);
            int[] upgraded = first.Players[0].Deck.Cards.Select((card, i) => (card, i)).Where(x => x.card.IsUpgraded).Select(x => x.i).ToArray();
            Assert.Single(upgraded);
            Assert.Equal(upgraded, second.Players[0].Deck.Cards.Select((card, i) => (card, i)).Where(x => x.card.IsUpgraded).Select(x => x.i));
            Assert.Equal(first.Players[0].Gold, second.Players[0].Gold);
            Assert.Equal(RngStates(first.Rng), RngStates(second.Rng));
            Assert.Equal(before, RunCloneGraphSnapshot.Capture(source));
            return;
        }
        if (scenario == "hidden-preroll")
        {
            var source = NewVisibleRun("in-room-" + scenario);
            source.Players[0].Gold = 500;
            var room = await EnterEventAsync<CrystalSphere>(source);
            await room.Event.ChooseOption(room.Event.CurrentOptions[0]);
            var original = Assert.IsType<CrystalSphere>(room.Event);
            await original.RevealAsync(1, 1, big: false);
            RunState variant = source.CloneExact();
            var variantGame = Assert.IsType<CrystalSphere>(Assert.IsType<EventRoom>(variant.CurrentRoom).Event).Game!;
            var concealedItems = (List<CrystalSphereItem>)typeof(CrystalSphereMinigame).GetField("_items", PrivateFields)!.GetValue(variantGame)!;
            concealedItems.Reverse();
            ((Rng)typeof(CrystalSphereMinigame).GetField("_rng", PrivateFields)!.GetValue(variantGame)!).NextInt(97);
            ChangeOnlyOldHiddenFuture(variant);
            var before = RunCloneGraphSnapshot.Capture(source);
            RunState first = source.CloneReseeded(91);
            RunState second = variant.CloneReseeded(91);
            var a = Assert.IsType<CrystalSphere>(Assert.IsType<EventRoom>(first.CurrentRoom).Event);
            var b = Assert.IsType<CrystalSphere>(Assert.IsType<EventRoom>(second.CurrentRoom).Event);
            Assert.Equal(original.UncoverFutureCost, a.UncoverFutureCost);
            Assert.Equal(original.Game!.DivinationCount, a.Game!.DivinationCount);
            Assert.Equal(original.Game.PlacedAllItems, a.Game.PlacedAllItems);
            Assert.Equal(original.Game.RevealedItems.Select(SphereItemState), a.Game.RevealedItems.Select(SphereItemState));
            for (int x = 0; x < 11; x++) for (int y = 0; y < 11; y++)
                Assert.Equal(original.Game.IsHidden(x, y), a.Game.IsHidden(x, y));
            Assert.Equal(a.Game.Items.Select(SphereItemState), b.Game!.Items.Select(SphereItemState));
            await a.RevealAsync(5, 5);
            await b.RevealAsync(5, 5);
            Assert.Equal(a.Game.RevealedItems.Select(SphereItemState), b.Game.RevealedItems.Select(SphereItemState));
            Assert.Equal(first.Players[0].Deck.Cards.Select(c => c.Id), second.Players[0].Deck.Cards.Select(c => c.Id));
            Assert.Equal(before, RunCloneGraphSnapshot.Capture(source));
            await AssertPreparedEventCounterfactualAsync<TheLanternKey>();
            await AssertPreparedEventCounterfactualAsync<PunchOff>();
            return;
        }
        if (scenario == "reward-visible")
        {
            var boundary = await RunCloneRoomFixture.CaptureAsync("reward", MapPointType.Monster, reseeded: true);
            await boundary.VerifyAsync();
            return;
        }
        ImaginationHiddenStateRegistry.ValidateCoverage();
        Type[] audited = [typeof(RunState), typeof(Player), typeof(ActDefinition), typeof(ActMap),
            typeof(StandardActMap), typeof(SpoilsActMap), typeof(GoldenPathActMap), typeof(MapPoint),
            typeof(Sts2Sim.Core.Odds.AbstractOdds), typeof(Sts2Sim.Core.Odds.UnknownMapPointOdds), typeof(Sts2Sim.Core.Odds.RunOddsSet)];
        foreach (Type type in audited)
            for (Type? parent = type; parent is not null && parent != typeof(object); parent = parent.BaseType)
                foreach (FieldInfo field in parent.GetFields(PrivateFields | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    Assert.True(RunCloneFieldRegistry.Fields.ContainsKey($"{parent.Name}.{field.Name}"), $"Unclassified clone field: {parent.Name}.{field.Name}");
        // Classification is accompanied by actual consumers, not merely a table-presence assertion.
        await AssertCallbackOwnedCardForkIsolationAsync();
        await RunCloneCurrentActHistoryFixture.VerifyAsync();
    }

    private const BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static RunState NewVisibleRun(string seed)
    {
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()], 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        player.Creature.SetMaxHpInternal(10_000);
        player.Creature.HealInternal(10_000);
        _ = run.CurrentAncientEventType; // Generate native future rooms before counterfactual mutation.
        return run;
    }
    private static async Task<EventRoom> EnterEventAsync<T>(RunState run) where T : EventModel
    {
        var room = new EventRoom(() => (EventModel)ModelDb.Get(typeof(T)).MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        return room;
    }
    private static string SphereItemState(CrystalSphereItem item) =>
        $"{item.Kind}|{item.Position}|{item.Width}|{item.Height}|{item.CardRarity}|{item.PotionRarity}|{item.IsBigGold}|{item.IsRevealed}|{item.RevealSubscriptions}";
    private static CombatState PreparedState(EventRoom room)
    {
        var prepared = (CombatRoom)typeof(EventRoom).GetField("_preparedCombatRoom", PrivateFields)!.GetValue(room)!;
        return (CombatState)typeof(CombatRoom).GetField("_preparedCombatState", PrivateFields)!.GetValue(prepared)!;
    }
    private static async Task AssertPreparedEventCounterfactualAsync<T>() where T : EventModel
    {
        var source = NewVisibleRun("prepared-" + typeof(T).Name);
        EventRoom sourceRoom = await EnterEventAsync<T>(source);
        RunState variant = source.CloneExact();
        EventRoom variantRoom = Assert.IsType<EventRoom>(variant.CurrentRoom);
        EncounterDefinition encounter = sourceRoom.Event.CanonicalEncounter!;
        var alternate = new CombatRoom(() => (encounter, encounter.CreateMonsters(new Rng(0xBEEFUL))), RoomType.Monster);
        alternate.Prepare(variant);
        typeof(EventRoom).GetField("_preparedCombatRoom", PrivateFields)!.SetValue(variantRoom, alternate);
        ChangeOnlyOldHiddenFuture(variant);
        string before = RunCloneGraphSnapshot.Capture(source);
        RunState first = source.CloneReseeded(97);
        RunState second = variant.CloneReseeded(97);
        CombatState a = PreparedState(Assert.IsType<EventRoom>(first.CurrentRoom));
        CombatState b = PreparedState(Assert.IsType<EventRoom>(second.CurrentRoom));
        Assert.Same(first, a.RunState);
        Assert.Same(first.Players[0].Creature, a.Players[0].Creature);
        Assert.Equal(a.Enemies.Select(e => (e.Monster!.GetType(), e.Monster.Rng.ToSerializable())), b.Enemies.Select(e => (e.Monster!.GetType(), e.Monster.Rng.ToSerializable())));
        if (typeof(T) == typeof(PunchOff))
        {
            int[] expected = encounter.CreateMonsters(new Rng(RoomFactory.EncounterMonsterSeed(first.Rng.Seed, first.TotalFloor, encounter)))
                .Select(entry => ((PunchConstruct)entry.Monster).StartingHpReduction).ToArray();
            Assert.Equal(expected, a.Enemies.Select(e => ((PunchConstruct)e.Monster!).StartingHpReduction));
            Assert.Equal(expected, b.Enemies.Select(e => ((PunchConstruct)e.Monster!).StartingHpReduction));
        }
        Assert.Equal(before, RunCloneGraphSnapshot.Capture(source));
    }
}


public sealed partial class RunStateCloneTests
{
    [Theory]
    [InlineData("reward", MapPointType.Monster)]
    [InlineData("shop", MapPointType.Shop)]
    [InlineData("rest", MapPointType.RestSite)]
    [InlineData("event", MapPointType.Unknown)]
    public async Task DirectCloneInsideRoom_ReproducesDecisionPointAndAfterstate(string roomKind, MapPointType pointType)
    {
        var boundary = await RunCloneRoomFixture.CaptureAsync(roomKind, pointType);
        await boundary.VerifyAsync();
    }

    [Theory]
    [InlineData("reward-card-choice")]
    [InlineData("shop-before-after")]
    [InlineData("rest")]
    [InlineData("event")]
    [InlineData("rejections")]
    public async Task CloneExact_StableConsumersPreserveIndependentWorlds(string scenario)
    {
        if (scenario == "rejections")
        {
            await AssertStableBoundaryRejectionsAsync();
            return;
        }
        string kind = scenario switch { "reward-card-choice" => "reward", "shop-before-after" => "shop", _ => scenario };
        MapPointType point = kind switch { "shop" => MapPointType.Shop, "rest" => MapPointType.RestSite, "event" => MapPointType.Unknown, _ => MapPointType.Monster };
        var boundary = await RunCloneRoomFixture.CaptureAsync(kind, point, captureAfterPurchase: scenario == "shop-before-after", captureCardChoice: scenario == "reward-card-choice");
        await boundary.VerifyAsync();
        if (scenario == "event") await AssertCallbackOwnedCardForkIsolationAsync();
    }

    private static async Task AssertStableBoundaryRejectionsAsync()
    {
        var active = NewVisibleRun("clone-active-reject");
        EncounterDefinition encounter = active.Act.MonsterEncounterCandidates[0];
        var combat = new CombatRoom(() => (encounter, encounter.CreateMonsters(new Rng(RoomFactory.EncounterMonsterSeed(active.Rng.Seed, active.TotalFloor, encounter)))), RoomType.Monster);
        active.PushRoom(combat);
        await combat.Enter(active);
        Assert.Equal(RunCloneRejectionReason.ActiveCombat, Assert.Throws<RunCloneNotSupportedException>(() => active.CloneExact()).Reason);
        Assert.Equal(RunCloneRejectionReason.ActiveCombat, Assert.Throws<RunCloneNotSupportedException>(() => active.CloneReseeded(375)).Reason);
        Assert.Same(combat, active.CurrentRoom);

        var pending = NewVisibleRun("clone-callback-reject");
        EventRoom room = await EnterEventAsync<TabletOfTruth>(pending);
        IReadOnlyList<EventOption> nativeOptions = room.Event.CurrentOptions;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var option = new EventOption("PENDING_CLONE_TEST", () => completion.Task);
        typeof(EventModel).GetField("<CurrentOptions>k__BackingField", PrivateFields)!.SetValue(room.Event, Array.AsReadOnly(new[] { option }));
        Task choice = room.Event.ChooseOption(option);
        try
        {
            Assert.False(choice.IsCompleted);
            Assert.Equal(RunCloneRejectionReason.PendingCallback, Assert.Throws<RunCloneNotSupportedException>(() => pending.CloneExact()).Reason);
            Assert.Equal(RunCloneRejectionReason.PendingCallback, Assert.Throws<RunCloneNotSupportedException>(() => pending.CloneReseeded(375)).Reason);
        }
        finally
        {
            completion.SetResult();
            await choice;
            typeof(EventModel).GetField("<CurrentOptions>k__BackingField", PrivateFields)!.SetValue(room.Event, nativeOptions);
        }
        RunState settled = pending.CloneExact();
        Assert.NotSame(room.Event, Assert.IsType<EventRoom>(settled.CurrentRoom).Event);
        var keyed = RunState.CreateKeyedForLabels("clone-open-scope", [new Overgrowth(), new Hive(), new Glory()], 10);
        keyed.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), keyed));
        using (keyed.Rng.BeginSemanticScope("test/open"))
        {
            Assert.Equal(RunCloneRejectionReason.OpenSemanticScope, Assert.Throws<RunCloneNotSupportedException>(() => keyed.CloneExact()).Reason);
            Assert.Equal(RunCloneRejectionReason.OpenSemanticScope, Assert.Throws<RunCloneNotSupportedException>(() => keyed.CloneReseeded(375)).Reason);
        }
        RunState closed = keyed.CloneExact();
        AssertExactRngSnapshots(keyed, closed);
    }
}

internal sealed class RunCloneRoomFixture
{
    private sealed record Fork(RunState World, int Step, int ShopChoices);
    private readonly RunState _source;
    private readonly string _kind;
    private readonly bool _reseeded;
    private readonly bool _afterPurchase;
    private readonly bool _cardChoice;
    private readonly List<Fork> _forks = [];
    private readonly List<string> _steps = [];
    private RunDriver.Result? _result;
    private RunCloneRoomFixture(RunState source, string kind, bool reseeded, bool afterPurchase, bool cardChoice) =>
        (_source, _kind, _reseeded, _afterPurchase, _cardChoice) = (source, kind, reseeded, afterPurchase, cardChoice);

    internal static async Task<RunCloneRoomFixture> CaptureAsync(string kind, MapPointType pointType,
        bool reseeded = false, bool captureAfterPurchase = false, bool captureCardChoice = false)
    {
        var source = new RunState("replay-" + kind, [new Overgrowth(), new Hive(), new Glory()], 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), source);
        source.AddPlayer(player);
        player.Creature.SetMaxHpInternal(10_000);
        player.Creature.HealInternal(10_000);
        player.Gold = 10_000;
        source.Map.StartingMapPoint.PointType = MapPointType.Monster;
        await new RunDriver(source, new FirstChoiceDecisionSource()).RunAsync(0);
        MapPoint next = source.Map.StartingMapPoint.Children.OrderBy(p => p.coord.col).First();
        next.PointType = pointType;
        if (kind != "reward")
        {
            // Explicit native room construction after the original four-row map fixture;
            // no claim that an Unknown roll naturally selected TabletOfTruth.
            source.AddVisitedMapCoord(next.coord);
            AbstractRoom room = kind switch
            {
                "shop" => new MerchantRoom(),
                "rest" => new RestSiteRoom(),
                "event" => new EventRoom(() => (EventModel)ModelDb.Event<TabletOfTruth>().MutableClone()),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            source.PushRoom(room);
            await room.Enter(source);
        }
        var fixture = new RunCloneRoomFixture(source, kind, reseeded, captureAfterPurchase, captureCardChoice);
        fixture._result = await new RunDriver(source,
            new TraceSource(source, fixture._steps, fixture.Before, buyOnce: true)).ContinueAsync(1);
        Assert.Equal(captureAfterPurchase ? 4 : 2, fixture._forks.Count);
        if (captureCardChoice) Assert.True(fixture._cardPointCaptured, "The native reward fixture did not reach displayed selectable cards.");
        return fixture;
    }

    private bool _cardPointCaptured;
    private void Before(string kind, object candidates, int priorShopChoices)
    {
        if (kind != _kind) return;
        if (_cardChoice)
        {
            var rewards = (RewardsSet)candidates;
            if (rewards.Card.IsResolved || rewards.Card.Options.Count == 0) return;
            _cardPointCaptured = true;
        }
        int wanted = _afterPurchase ? 4 : 2;
        if (_forks.Count >= wanted) return;
        string before = RunCloneGraphSnapshot.Capture(_source);
        for (int sibling = 0; sibling < 2; sibling++)
        {
            RunState child = _reseeded ? _source.CloneReseeded(91) : _source.CloneExact();
            if (!_reseeded) Assert.Equal(before, RunCloneGraphSnapshot.Capture(child));
            _forks.Add(new Fork(child, _steps.Count, priorShopChoices));
        }
        Assert.Equal(before, RunCloneGraphSnapshot.Capture(_source));
    }

    internal async Task VerifyAsync()
    {
        foreach (Fork fork in _forks)
        {
            var steps = new List<string>();
            RunDriver.Result result = await new RunDriver(fork.World,
                new TraceSource(fork.World, steps, null, fork.ShopChoices, buyOnce: true)).ContinueAsync(1);
            Assert.Equal(_result!.Outcome, result.Outcome);
            Assert.Equal(_result.FinalPlayerHp, result.FinalPlayerHp);
            if (!_reseeded)
            {
                Assert.Equal(_steps.Skip(fork.Step), steps);
                Assert.Equal(RunCloneGraphSnapshot.Capture(_source), RunCloneGraphSnapshot.Capture(fork.World));
            }
        }
        for (int pair = 0; pair < _forks.Count; pair += 2)
        {
            RunState child = _forks[pair].World;
            RunState sibling = _forks[pair + 1].World;
            Assert.Equal(RunCloneGraphSnapshot.Capture(child), RunCloneGraphSnapshot.Capture(sibling));
            int sourceGold = _source.Players[0].Gold;
            int siblingGold = sibling.Players[0].Gold;
            child.Players[0].Gold++;
            Assert.Equal(sourceGold, _source.Players[0].Gold);
            Assert.Equal(siblingGold, sibling.Players[0].Gold);
            _source.Players[0].Gold++;
            Assert.Equal(siblingGold, sibling.Players[0].Gold);
        }
    }

    internal sealed class TraceSource(RunState run, List<string> steps,
        Action<string, object, int>? before, int priorShopChoices = 0, bool buyOnce = false) : IRunDecisionSource
    {
        private readonly IRunDecisionSource _inner = new FirstChoiceDecisionSource();
        private int _shopChoices = priorShopChoices;
        private async Task<T> Select<T>(string kind, object candidates, Func<Task<T>> action)
        {
            before?.Invoke(kind, candidates, _shopChoices);
            string snapshot = RunCloneGraphSnapshot.Capture(run, candidates);
            T selected = await action();
            steps.Add(kind + "\n" + snapshot + "\n" + RunCloneGraphSnapshot.Capture(run, candidates, selected));
            return selected;
        }
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => Select("map", options, () => _inner.ChooseMapPointAsync(options));
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => Select("combat", state, () => _inner.ChooseCombatActionAsync(state));
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) => Select("cards", request, () => _inner.ChooseCardsAsync(request));
        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) => Select("reward", rewards, () => _inner.ChooseRewardActionAsync(rewards));
        public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) => Select("shop", inventory, () =>
        {
            if (!buyOnce) return _inner.ChooseShopActionAsync(inventory, player);
            ShopDecision selected = _shopChoices++ == 0
                ? ShopDecisionCandidates.Build(inventory, player).Select(c => c.Decision).OfType<ShopDecision.BuyCard>().First()
                : new ShopDecision.Leave();
            return Task.FromResult(selected);
        });
        public Task<CustomEventDecision> ChooseCustomEventActionAsync(EventModel @event) => Select("custom-event", @event, () => _inner.ChooseCustomEventActionAsync(@event));
        public Task<RestSiteDecision> ChooseRestSiteActionAsync(Player player, IReadOnlyList<RestSiteDecision> candidates) => Select("rest", candidates, () => _inner.ChooseRestSiteActionAsync(player, candidates));
        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) => Select("event", options, () => _inner.ChooseEventOptionAsync(options));
    }
}


// The approved new six-row private-graph Theory; merge into the single final tests input.
public sealed partial class RunStateCloneTests
{
    [Theory]
    [InlineData("book-private-counter")]
    [InlineData("paels-held-card")]
    [InlineData("provenance-attachments")]
    [InlineData("map-quest-alias")]
    [InlineData("pending-transplant")]
    [InlineData("player-flags-orb-slots")]
    public async Task CloneExact_PrivateOwnedGraphConsumersAreIsolated(string scenario)
    {
        switch (scenario)
        {
            case "book-private-counter": await AssertBookPrivateCounterGraphAsync(); break;
            case "paels-held-card": await AssertPaelsHeldCardGraphAsync(); break;
            case "provenance-attachments": await AssertCardProvenanceAttachmentsAsync(); break;
            case "map-quest-alias": AssertMapQuestGraphAsync(); break;
            case "pending-transplant": AssertPendingTransplantRejection(); break;
            case "player-flags-orb-slots": RunClonePlayerFlagsFixture.Verify(); break;
        }
    }

    // Insert this helper into the approved private-graph Theory's existing Book row.
    private static async Task AssertBookPrivateCounterGraphAsync()
    {
        var run = new RunState("book-full-private-counter", [new Overgrowth(), new Hive(), new Glory()], 10);
        var sourcePlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(sourcePlayer);
        var sourceBook = (Sts2Sim.Core.Models.Relics.BookOfFiveRings)
            ModelDb.Relic<Sts2Sim.Core.Models.Relics.BookOfFiveRings>().MutableClone();
        sourceBook.AssignOwner(sourcePlayer);
        sourcePlayer.AddRelicInternal(sourceBook);
        FieldInfo counter = typeof(Sts2Sim.Core.Models.Relics.BookOfFiveRings)
            .GetField("_cardsAdded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        counter.SetValue(sourceBook, 104);
        sourcePlayer.Creature.SetCurrentHpInternal(sourcePlayer.Creature.MaxHp - 30m);
        RunState child = run.CloneExact();
        RunState sibling = run.CloneExact();
        var childBook = child.Players[0].Relics.OfType<Sts2Sim.Core.Models.Relics.BookOfFiveRings>().Single();
        var siblingBook = sibling.Players[0].Relics.OfType<Sts2Sim.Core.Models.Relics.BookOfFiveRings>().Single();
        Assert.Equal(104, (int)counter.GetValue(childBook)!);
        Assert.Equal(104, (int)counter.GetValue(siblingBook)!);
        Assert.Equal(sourceBook.DisplayAmount, childBook.DisplayAmount);
        Assert.NotSame(sourceBook, childBook);
        Assert.NotSame(childBook, siblingBook);
        Assert.Same(child.Players[0], childBook.Owner);
        decimal sourceHp = sourcePlayer.Creature.CurrentHp;
        decimal siblingHp = sibling.Players[0].Creature.CurrentHp;
        var added = (CardModel)ModelDb.Card<StrikeSilent>().MutableClone();
        added.AssignOwner(child.Players[0]);
        child.Players[0].Deck.AddInternal(added);
        // Any prior pile other than Deck triggers the documented add-to-deck consumer.
        await childBook.AfterCardChangedPiles(added, default, null);
        Assert.Equal(105, (int)counter.GetValue(childBook)!);
        Assert.Equal(sourceHp + 20m, child.Players[0].Creature.CurrentHp);
        Assert.Equal(sourceHp, sourcePlayer.Creature.CurrentHp);
        Assert.Equal(siblingHp, sibling.Players[0].Creature.CurrentHp);
        Assert.Equal(104, (int)counter.GetValue(sourceBook)!);
        Assert.Equal(104, (int)counter.GetValue(siblingBook)!);
        counter.SetValue(sourceBook, 109);
        Assert.Equal(105, (int)counter.GetValue(childBook)!);
        Assert.Equal(104, (int)counter.GetValue(siblingBook)!);
    }
    
    // Insert into the approved private-graph Theory's existing PaelsTooth row.
    private static async Task AssertPaelsHeldCardGraphAsync()
    {
        var run = new RunState("paels-private-held-card", [new Overgrowth(), new Hive(), new Glory()], 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var tooth = (Sts2Sim.Core.Models.Relics.PaelsTooth)
            ModelDb.Relic<Sts2Sim.Core.Models.Relics.PaelsTooth>().MutableClone();
        tooth.AssignOwner(player);
        player.AddRelicInternal(tooth);
        var held = (CardModel)ModelDb.Card<StrikeSilent>().MutableClone();
        held.AssignOwner(player);
        FieldInfo storage = typeof(Sts2Sim.Core.Models.Relics.PaelsTooth)
            .GetField("_removedCards", BindingFlags.Instance | BindingFlags.NonPublic)!;
        storage.SetValue(tooth, new List<CardModel> { held });
        RunState child = run.CloneExact();
        RunState sibling = run.CloneExact();
        var childTooth = child.Players[0].Relics.OfType<Sts2Sim.Core.Models.Relics.PaelsTooth>().Single();
        var siblingTooth = sibling.Players[0].Relics.OfType<Sts2Sim.Core.Models.Relics.PaelsTooth>().Single();
        var childStorage = (List<CardModel>)storage.GetValue(childTooth)!;
        var siblingStorage = (List<CardModel>)storage.GetValue(siblingTooth)!;
        CardModel childHeld = Assert.Single(childStorage);
        CardModel siblingHeld = Assert.Single(siblingStorage);
        Assert.NotSame(held, childHeld);
        Assert.NotSame(childHeld, siblingHeld);
        Assert.Same(child.Players[0], childHeld.Owner);
        Assert.Same(sibling.Players[0], siblingHeld.Owner);
        int sourceDeckCount = player.Deck.Cards.Count;
        int siblingDeckCount = sibling.Players[0].Deck.Cards.Count;
        await childTooth.AfterCombatEnd();
        Assert.Contains(childHeld, child.Players[0].Deck.Cards);
        Assert.True(childHeld.IsUpgraded);
        Assert.Empty(childStorage);
        Assert.Equal(sourceDeckCount, player.Deck.Cards.Count);
        Assert.Equal(siblingDeckCount, sibling.Players[0].Deck.Cards.Count);
        Assert.False(held.IsUpgraded);
        Assert.False(siblingHeld.IsUpgraded);
        Assert.Same(held, Assert.Single((List<CardModel>)storage.GetValue(tooth)!));
        Assert.Same(siblingHeld, Assert.Single(siblingStorage));
        await tooth.AfterCombatEnd();
        Assert.Contains(held, player.Deck.Cards);
        Assert.Empty((List<CardModel>)storage.GetValue(tooth)!);
        Assert.Equal(siblingDeckCount, sibling.Players[0].Deck.Cards.Count);
        Assert.Same(siblingHeld, Assert.Single(siblingStorage));
    }
    
    // Embed these helpers in the already approved provenance/map/transplant rows; no new Theory rows.
    private static async Task AssertCardProvenanceAttachmentsAsync()
    {
        var run = new RunState("card-provenance-attachments", [new Overgrowth(), new Hive(), new Glory()], 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var origin = (CardModel)ModelDb.Card<StrikeSilent>().MutableClone();
        origin.AssignOwner(player);
        var enchantment = (Sts2Sim.Core.Models.Enchantments.Clone)
            ModelDb.Get(typeof(Sts2Sim.Core.Models.Enchantments.Clone)).MutableClone();
        enchantment.AssignMagnitude(7m);
        origin.AttachEnchantment(enchantment);
        var hexed = (Sts2Sim.Core.Models.Afflictions.Hexed)
            ModelDb.Get(typeof(Sts2Sim.Core.Models.Afflictions.Hexed)).MutableClone();
        origin.AttachAffliction(hexed, 3m);
        CardModel first = origin.CreateClone();
        CardModel second = origin.CreateClone();
        player.Deck.AddInternal(first);
        player.Deck.AddInternal(second);
        RunState child = run.CloneExact();
        RunState sibling = run.CloneExact();
        CardModel[] childCopies = child.Players[0].Deck.Cards.Where(card => card.CloneOf is not null).ToArray();
        CardModel[] siblingCopies = sibling.Players[0].Deck.Cards.Where(card => card.CloneOf is not null).ToArray();
        CardModel childOrigin = childCopies[0].CloneOf!;
        CardModel siblingOrigin = siblingCopies[0].CloneOf!;
        Assert.Same(childOrigin, childCopies[1].CloneOf);
        Assert.Same(siblingOrigin, siblingCopies[1].CloneOf);
        Assert.NotSame(origin, childOrigin);
        Assert.NotSame(childOrigin, siblingOrigin);
        Assert.Same(child.Players[0], childOrigin.Owner);
        Assert.Same(sibling.Players[0], siblingOrigin.Owner);
        EnchantmentModel childEnchantment = Assert.Single(childOrigin.Enchantments);
        EnchantmentModel siblingEnchantment = Assert.Single(siblingOrigin.Enchantments);
        Assert.Same(childOrigin, childEnchantment.Owner);
        Assert.Same(siblingOrigin, siblingEnchantment.Owner);
        Assert.Equal(7m, childEnchantment.Magnitude);
        childEnchantment.AssignMagnitude(11m);
        Assert.Equal(7m, enchantment.Magnitude);
        Assert.Equal(7m, siblingEnchantment.Magnitude);
        childOrigin.Upgrade();
        Assert.True(childCopies[1].CloneOf!.IsUpgraded);
        Assert.False(origin.IsUpgraded);
        Assert.False(siblingOrigin.IsUpgraded);
        var childHexed = Assert.IsType<Sts2Sim.Core.Models.Afflictions.Hexed>(childOrigin.Affliction);
        await childHexed.AfterCardEnteredCombat(childOrigin);
        Assert.Null(childOrigin.Affliction);
        Assert.Same(hexed, origin.Affliction);
        Assert.IsType<Sts2Sim.Core.Models.Afflictions.Hexed>(siblingOrigin.Affliction);
        enchantment.AssignMagnitude(17m);
        Assert.Equal(11m, childEnchantment.Magnitude);
        Assert.Equal(7m, siblingEnchantment.Magnitude);
    }
    
    private static void AssertMapQuestGraphAsync()
    {
        var run = new RunState("map-quest-shared-root", [new Overgrowth(), new Hive(), new Glory()], 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var quest = (CardModel)ModelDb.Card<Sts2Sim.Core.Models.Cards.LanternKey>().MutableClone();
        quest.AssignOwner(player);
        player.Deck.AddInternal(quest);
        MapPoint sourceStart = run.Map.StartingMapPoint;
        MapPoint sourceBoss = run.Map.BossMapPoint;
        sourceStart.AddQuest(quest);
        sourceBoss.AddQuest(quest);
        RunState child = run.CloneExact();
        RunState sibling = run.CloneExact();
        CardModel childQuest = child.Players[0].Deck.Cards.Single(card => card.GetType() == quest.GetType());
        CardModel siblingQuest = sibling.Players[0].Deck.Cards.Single(card => card.GetType() == quest.GetType());
        Assert.Same(childQuest, Assert.Single(child.Map.StartingMapPoint.Quests));
        Assert.Same(childQuest, Assert.Single(child.Map.BossMapPoint.Quests));
        Assert.Same(siblingQuest, Assert.Single(sibling.Map.StartingMapPoint.Quests));
        Assert.NotSame(quest, childQuest);
        Assert.NotSame(childQuest, siblingQuest);
        Assert.Same(child.Players[0], childQuest.Owner);
        child.Map.StartingMapPoint.RemoveQuest(childQuest);
        Assert.Empty(child.Map.StartingMapPoint.Quests);
        Assert.Same(childQuest, Assert.Single(child.Map.BossMapPoint.Quests));
        Assert.Same(quest, Assert.Single(sourceStart.Quests));
        Assert.Same(siblingQuest, Assert.Single(sibling.Map.StartingMapPoint.Quests));
        sourceBoss.RemoveQuest(quest);
        Assert.Empty(sourceBoss.Quests);
        Assert.Same(childQuest, Assert.Single(child.Map.BossMapPoint.Quests));
        Assert.Same(siblingQuest, Assert.Single(sibling.Map.BossMapPoint.Quests));
    }
    
    private static void AssertPendingTransplantRejection()
    {
        var run = new RunState("pending-transplant-rejection", [new Overgrowth(), new Hive(), new Glory()], 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        EncounterDefinition encounter = run.Acts[0].MonsterEncounterCandidates[0];
        run.SetInjectedEncounterForTransplant(RoomType.Monster, encounter.IdEntry);
        var exact = Assert.Throws<RunCloneNotSupportedException>(() => run.CloneExact());
        var reseeded = Assert.Throws<RunCloneNotSupportedException>(() => run.CloneReseeded(0x375UL));
        Assert.Equal(RunCloneRejectionReason.PendingTransplant, exact.Reason);
        Assert.Equal(RunCloneRejectionReason.PendingTransplant, reseeded.Reason);
        // Rejection must leave the queued encounter available to its real consumer.
        Assert.Same(encounter, run.PullNextEncounterForTransplant(RoomType.Monster));
    }
    
}


// To be called by the player-flags row of the approved six-row private-graph Theory.
// This is a prepared fixture, not another Theory or a standalone smoke command.
internal static class RunClonePlayerFlagsFixture
{
    internal static void Verify()
    {
        var sourceRun = new RunState("issue375-player-flags", new Overgrowth());
        Player source = Player.CreateForNewRun(ModelDb.Character<Regent>(), sourceRun);
        sourceRun.AddPlayer(source);
        source.CanUseOrRemovePotions = false;
        source.BaseOrbSlotCount = 7;

        RunState childRun = sourceRun.CloneExact();
        RunState siblingRun = sourceRun.CloneExact();
        Player child = childRun.Players[0];
        Player sibling = siblingRun.Players[0];

        Assert.False(child.CanUseOrRemovePotions);
        Assert.False(sibling.CanUseOrRemovePotions);
        Assert.Equal(7, child.BaseOrbSlotCount);
        Assert.Equal(7, sibling.BaseOrbSlotCount);
        Assert.Same(childRun, child.RunState);
        Assert.Same(siblingRun, sibling.RunState);

        child.CanUseOrRemovePotions = true;
        child.BaseOrbSlotCount = 2;
        Assert.False(source.CanUseOrRemovePotions);
        Assert.False(sibling.CanUseOrRemovePotions);
        Assert.Equal(7, source.BaseOrbSlotCount);
        Assert.Equal(7, sibling.BaseOrbSlotCount);
        source.CanUseOrRemovePotions = true;
        source.BaseOrbSlotCount = 4;
        Assert.False(sibling.CanUseOrRemovePotions);
        Assert.Equal(7, sibling.BaseOrbSlotCount);
        Assert.Equal(2, child.BaseOrbSlotCount);
    }
}


// Invoked inside the existing no-leak Theory row, not an additional test method/parameter row.
internal static class RunCloneCurrentActHistoryFixture
{
    internal static async Task VerifyAsync()
    {
        var start = new RunState("native-archive-act-entry", [new Overgrowth(), new Hive()], 10);
        start.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), start));
        start.AddVisitedMapCoord(start.Map.StartingMapPoint.coord);
        start.Odds.UnknownMapPoint.SetBaseOdds(RoomType.Monster, 0.17f);
        start.Odds.UnknownMapPoint.SetBaseOdds(RoomType.Elite, 0.07f);
        start.Odds.UnknownMapPoint.ResetToBase();
        // Cumulative floats are not an allowed source for a no-Unknown act's Reseed.
        start.Odds.UnknownMapPoint.MonsterOdds = 0.91f;
        start.Odds.UnknownMapPoint.OverrideCurrentValue(0.73f);
        RunState startExact = start.CloneExact();
        RunState startReseeded = start.CloneReseeded(375);
        Assert.Equal(0.91f, startExact.Odds.UnknownMapPoint.MonsterOdds);
        Assert.Equal(0.73f, startExact.Odds.UnknownMapPoint.CurrentValue);
        Assert.Equal(0.17f, startReseeded.Odds.UnknownMapPoint.MonsterOdds);
        Assert.Equal(0.07f, startReseeded.Odds.UnknownMapPoint.EliteOdds);
        startExact.Odds.UnknownMapPoint.ResetToBase();
        Assert.Equal(0.17f, startExact.Odds.UnknownMapPoint.MonsterOdds);
        Assert.Equal(0.91f, start.Odds.UnknownMapPoint.MonsterOdds);

        var observed = new RunState("known-unknown-history", [new Overgrowth(), new Hive()], 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), observed);
        observed.AddPlayer(player);
        foreach (RoomType type in new[] { RoomType.Monster, RoomType.Elite, RoomType.Treasure, RoomType.Shop })
            observed.Odds.UnknownMapPoint.SetBaseOdds(type, -1f);
        observed.Odds.UnknownMapPoint.ResetToBase();
        await new RunDriver(observed, new UnknownThenLeavePolicy(), null, null, false,
            _ => new EventRoom(() => (EventModel)ModelDb.Event<TabletOfTruth>().MutableClone()))
            .RunAsync(1);
        UnknownMapPointVisit visit = Assert.Single(observed.VisibleUnknownMapPointVisits);
        Assert.Equal(RoomType.Event, visit.ActualRoomType);
        RunState exact = observed.CloneExact();
        RunState reseeded = observed.CloneReseeded(376);
        Assert.Equal(observed.VisibleUnknownMapPointVisits, exact.VisibleUnknownMapPointVisits);
        Assert.NotSame(observed.VisibleUnknownMapPointVisits, exact.VisibleUnknownMapPointVisits);
        Assert.Equal(observed.Odds.UnknownMapPoint.MonsterOdds, reseeded.Odds.UnknownMapPoint.MonsterOdds);
        Assert.Equal(observed.Odds.UnknownMapPoint.EliteOdds, reseeded.Odds.UnknownMapPoint.EliteOdds);
        Assert.Equal(observed.Odds.UnknownMapPoint.TreasureOdds, reseeded.Odds.UnknownMapPoint.TreasureOdds);
        Assert.Equal(observed.Odds.UnknownMapPoint.ShopOdds, reseeded.Odds.UnknownMapPoint.ShopOdds);
        observed.Odds.UnknownMapPoint.MonsterOdds = 0.88f;
        RunState counterfactual = observed.CloneReseeded(376);
        Assert.Equal(reseeded.Odds.UnknownMapPoint.MonsterOdds, counterfactual.Odds.UnknownMapPoint.MonsterOdds);
        Assert.Equal(reseeded.Odds.UnknownMapPoint.ShopOdds, counterfactual.Odds.UnknownMapPoint.ShopOdds);

        // Model an older/migrated format which retained entered-room evidence but lost result records.
        var records = (List<UnknownMapPointVisit>)typeof(RunState)
            .GetField("_visibleMapVisits", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(observed)!;
        records.Clear();
        Assert.Throws<MissingVisibleUnknownHistoryException>(() => observed.CloneReseeded(376));
        RunState missingExact = observed.CloneExact();
        Assert.Equal(0.88f, missingExact.Odds.UnknownMapPoint.MonsterOdds);
        missingExact.Odds.UnknownMapPoint.SetBaseOdds(RoomType.Monster, 0.25f);
        missingExact.Odds.UnknownMapPoint.ResetToBase();
        Assert.Equal(0.25f, missingExact.Odds.UnknownMapPoint.MonsterOdds);
        Assert.Equal(0.88f, observed.Odds.UnknownMapPoint.MonsterOdds);
        observed.AdvanceToNextAct();
        Assert.Empty(observed.VisibleUnknownMapPointVisits);
        RunState nextAct = observed.CloneReseeded(377);
        Assert.Equal(observed.Odds.UnknownMapPoint.PublicBaseRules.Monster, nextAct.Odds.UnknownMapPoint.MonsterOdds);

        // Fixed real setup which naturally assigns the shared Darv to the second act.
        var ancientHistory = new RunState("seen-shared-ancient-5",
            [new Overgrowth(), new Hive(), new Glory()], 10);
        ancientHistory.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), ancientHistory));
        ancientHistory.AdvanceToNextAct();
        await new RunDriver(ancientHistory, new FirstChoiceDecisionSource()).ContinueAsync(0);
        Assert.Null(ancientHistory.CurrentRoom); // Real Ancient options consumed; room has exited.
        Assert.Equal(typeof(Darv), ancientHistory.CurrentAncientEventType);
        RunState future = ancientHistory.CloneReseeded(16);
        Assert.Equal(typeof(Darv), future.CurrentAncientEventType); // Already observed, even after exit.
        future.AdvanceToNextAct();
        Assert.NotEqual(typeof(Darv), future.CurrentAncientEventType);
    }

    private sealed class UnknownThenLeavePolicy : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options)
        {
            MapPoint point = options[0];
            point.PointType = MapPointType.Unknown;
            return Task.FromResult(point);
        }

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
            Task.FromResult(options.Last(option => option.IsEnabled));
    }
}


// To be invoked by the existing reseeded no-leak row after extracting CloneReshuffled.
// This fixture perturbs only hidden order; it does not add a Theory or consume a run RNG.
internal static class RunCloneRelicBagCounterfactualFixture
{
    internal static void Verify(RelicGrabBag source)
    {
        RelicBagSnapshot before = source.ExportForTransplant();
        var reversed = new RelicBagSnapshot(before.Buckets.Reverse().ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Reverse().ToArray(),
            StringComparer.Ordinal));
        RelicGrabBag hiddenVariant = RelicGrabBag.ImportForTransplant(reversed, "clone.relicBag");

        RelicBagSnapshot left = source.CloneReshuffled(new Rng(375UL)).ExportForTransplant();
        RelicBagSnapshot right = hiddenVariant.CloneReshuffled(new Rng(375UL)).ExportForTransplant();
        Assert.Equal(left.Buckets.Keys.ToArray(), right.Buckets.Keys.ToArray());
        foreach (string rarity in before.Buckets.Keys)
        {
            Assert.Equal(left.Buckets[rarity].ToArray(), right.Buckets[rarity].ToArray());
            Assert.Equal(before.Buckets[rarity].Order(StringComparer.Ordinal).ToArray(),
                left.Buckets[rarity].Order(StringComparer.Ordinal).ToArray());
        }

        RelicBagSnapshot after = source.ExportForTransplant();
        Assert.Equal(before.Buckets.Keys.ToArray(), after.Buckets.Keys.ToArray());
        foreach (string rarity in before.Buckets.Keys)
            Assert.Equal(before.Buckets[rarity].ToArray(), after.Buckets[rarity].ToArray());
    }
}

public sealed partial class RunStateCloneTests
{
// Integrate this helper into the already approved exact stable-room "event" row.
// It adds no Theory/Fact/parameter row and exercises a real option/CardCmd consumer.
private static async Task AssertCallbackOwnedCardForkIsolationAsync()
{
    var source = new RunState("clone-callback-private-card", [new Overgrowth(), new Hive(), new Glory()], 10);
    Player sourcePlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), source);
    source.AddPlayer(sourcePlayer);
    var room = new EventRoom(() => (EventModel)ModelDb.Event<TabletOfTruth>().MutableClone());
    source.PushRoom(room);
    await room.Enter(source);

    var hidden = (CardModel)ModelDb.Card<StrikeSilent>().MutableClone();
    hidden.AssignOwner(sourcePlayer);
    Assert.DoesNotContain(sourcePlayer.Deck.Cards, card => ReferenceEquals(hidden, card));
    FieldInfo options = typeof(EventModel).GetField("<CurrentOptions>k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic)!;
    options.SetValue(room.Event, Array.AsReadOnly(new[] { UpgradeCapturedCardOption(hidden) }));

    RunState child = source.CloneExact();
    RunState sibling = source.CloneExact();
    var childRoom = Assert.IsType<EventRoom>(child.CurrentRoom);
    var siblingRoom = Assert.IsType<EventRoom>(sibling.CurrentRoom);
    CardModel childCaptured = CapturedUpgradeCard(childRoom.Event.CurrentOptions[0]);
    CardModel siblingCaptured = CapturedUpgradeCard(siblingRoom.Event.CurrentOptions[0]);
    Assert.NotSame(hidden, childCaptured);
    Assert.NotSame(hidden, siblingCaptured);
    Assert.NotSame(childCaptured, siblingCaptured);
    Assert.Same(child.Players[0], childCaptured.Owner);
    Assert.Same(sibling.Players[0], siblingCaptured.Owner);
    Assert.False(hidden.IsUpgraded);
    Assert.False(childCaptured.IsUpgraded);
    Assert.False(siblingCaptured.IsUpgraded);

    await childRoom.Event.ChooseOption(childRoom.Event.CurrentOptions[0]);
    Assert.True(childCaptured.IsUpgraded);
    Assert.False(hidden.IsUpgraded);
    Assert.False(siblingCaptured.IsUpgraded);
    Assert.Equal(room.Id, childRoom.Id);
    Assert.Equal(room.Id, siblingRoom.Id);

    await room.Event.ChooseOption(room.Event.CurrentOptions[0]);
    Assert.True(hidden.IsUpgraded);
    Assert.True(childCaptured.IsUpgraded);
    Assert.False(siblingCaptured.IsUpgraded);
    await siblingRoom.Event.ChooseOption(siblingRoom.Event.CurrentOptions[0]);
    Assert.True(siblingCaptured.IsUpgraded);
}

private static Sts2Sim.Core.Events.EventOption UpgradeCapturedCardOption(CardModel captured) =>
    new("CLONE_PRIVATE_CARD_UPGRADE", () =>
    {
        Sts2Sim.Core.Commands.CardCmd.Upgrade(captured);
        return Task.CompletedTask;
    });

private static CardModel CapturedUpgradeCard(Sts2Sim.Core.Events.EventOption option)
{
    var callback = (Func<Task>)typeof(Sts2Sim.Core.Events.EventOption).GetField("_onChosen",
        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(option)!;
    object target = callback.Target!;
    FieldInfo captured = target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public
        | BindingFlags.NonPublic).Single(field => field.FieldType == typeof(CardModel));
    return (CardModel)captured.GetValue(target)!;
}

}

// Exact-world private state/alias snapshot used by the approved six consumer Theories.
// Snapshots contain strings only, never mutable source-world entities or live delegates.
internal static class RunCloneGraphSnapshot
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FieldInfo[]> Fields = new();

    internal static string Capture(RunState run, object? candidates = null, object? selected = null)
    {
        var text = new StringBuilder();
        var identities = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<object>();
        Append(run);
        Append(candidates);
        Append(selected);
        while (pending.TryDequeue(out object? item))
        {
            int id = identities[item];
            Type type = item.GetType();
            text.Append('\n').Append(id).Append(':').Append(type.FullName).Append('{');
            if (item is Delegate callback)
            {
                foreach (Delegate invocation in Delegate.EnumerateInvocationList(callback))
                {
                    text.Append(invocation.GetType().FullName).Append(':')
                        .Append(invocation.Method.DeclaringType?.FullName).Append(':')
                        .Append(invocation.Method.Name).Append(':').Append(invocation.Method.MetadataToken);
                    Append(invocation.Target);
                }
            }
            else if (item is IEnumerable sequence)
            {
                // Enumerate logical slots in their consumer order, not Dictionary hash buckets
                // or the private layout/capacity of a .NET collection.
                foreach (object? value in sequence) Append(value);
            }
            else
            {
                foreach (FieldInfo field in InstanceFields(type))
                {
                    if (IsRuntimeControl(field)) continue;
                    text.Append(field.DeclaringType?.FullName).Append('.').Append(field.Name).Append('=');
                    Append(field.GetValue(item));
                }
            }
            text.Append('}');
        }
        // Retain every UTF-16 code unit, including unpaired surrogates. This is
        // lossless storage, not a digest or a reduced state projection.
        // Write builder chunks directly so a second full-size string is not allocated.
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            foreach (ReadOnlyMemory<char> chunk in text.GetChunks())
                deflate.Write(MemoryMarshal.AsBytes(chunk.Span));
        return Convert.ToBase64String(compressed.GetBuffer(), 0, checked((int)compressed.Length));

        void Append(object? value)
        {
            if (value is null) { text.Append("null;"); return; }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal or Guid or DateTime or DateTimeOffset or TimeSpan)
            {
                text.Append(type.FullName).Append(':')
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';');
                return;
            }
            if (value is string stringValue)
            {
                text.Append('s').Append(stringValue.Length).Append(':').Append(stringValue).Append(';');
                return;
            }
            if (value is Type metadata)
            {
                text.Append("type:").Append(metadata.FullName).Append(';');
                return;
            }
            if (value is AbstractModel { IsCanonical: true } canonical)
            {
                text.Append("canonical:").Append(type.FullName).Append(':').Append(canonical.Id).Append(';');
                return;
            }
            if (value is MemberInfo member)
            {
                text.Append("member:").Append(member.DeclaringType?.FullName).Append(':')
                    .Append(member.Name).Append(':').Append(member.MetadataToken).Append(';');
                return;
            }
            if (value is Task || value is IAsyncStateMachine)
                throw new InvalidOperationException($"Unclassified runtime handle in exact-world snapshot: {type.FullName}.");
            if (value is StringComparer || ReferenceEquals(value, ReferenceEqualityComparer.Instance)
                || type.Assembly == typeof(EqualityComparer<>).Assembly && type.Namespace == "System.Collections.Generic"
                    && type.GetInterfaces().Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IEqualityComparer<>) || i.GetGenericTypeDefinition() == typeof(IComparer<>))))
            {
                text.Append("comparer:").Append(type.FullName).Append(';');
                return;
            }
            if (type != typeof(object) && type.Assembly != typeof(RunState).Assembly && value is not IEnumerable && value is not Delegate
                && !type.IsValueType && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                throw new InvalidOperationException($"Unclassified mutable graph data: {type.FullName}.");
            if (type.IsValueType)
            {
                text.Append(type.FullName).Append('(');
                foreach (FieldInfo field in InstanceFields(type))
                {
                    text.Append(field.Name).Append('=');
                    Append(field.GetValue(value));
                }
                text.Append(");");
                return;
            }
            if (!identities.TryGetValue(value, out int identity))
            {
                identity = identities.Count;
                identities.Add(value, identity);
                pending.Enqueue(value);
            }
            text.Append('@').Append(identity).Append(';');
        }
    }

    private static bool IsRuntimeControl(FieldInfo field) =>
        field.Name is "_stateLock" or "_lifecycleLock" or "_rewardLock" or "_takeLock" or "_resolutionLock"
            or "_purchaseGate" or "_revealGate" or "_interactionGate" or "_insideLifecycle" or "_observer"
            or "_cardSelectionSource" or "<CardSelectionSource>k__BackingField"
            or "_activeChoiceTask" or "_activeChoiceOption" or "_activeChoicePageVersion"
            or "_takeTask" or "_resolutionTask" or "_outcomeTask" or "_exitTask" or "_completionTask"
        || field.DeclaringType == typeof(CombatState) && field.Name == "_hookListenerSnapshotCache"
        || field.DeclaringType == typeof(AbstractModel) && field.Name == "ExecutionFinished"
        || field.DeclaringType == typeof(RelicModel) && field.Name == "Flashed";

    private static FieldInfo[] InstanceFields(Type type) => Fields.GetOrAdd(type, static root =>
    {
        var fields = new List<FieldInfo>();
        for (Type? current = root; current is not null && current != typeof(object); current = current.BaseType)
            fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        return fields.OrderBy(field => field.DeclaringType?.FullName, StringComparer.Ordinal)
            .ThenBy(field => field.Name, StringComparer.Ordinal).ToArray();
    });
}

