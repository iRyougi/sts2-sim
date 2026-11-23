using Sts2Sim.TestSupport;
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
using System.Reflection;
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
        HiddenFutureCounterfactual.Change(hiddenVariant);
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
                List<Type> firstSequence = HiddenFutureCounterfactual.PrivateList<Type>(first, "_eventSequence");
                List<Type> secondSequence = HiddenFutureCounterfactual.PrivateList<Type>(second, "_eventSequence");
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
    [InlineData("sphere-late-1")]
    [InlineData("sphere-late-2")]
    [InlineData("reward-visible")]
    [InlineData("registry-coverage")]
    public async Task CloneReseeded_InRoomUsesOnlyVisibleConditions(string scenario)
    {
        if (scenario == "tablet-decipher")
        {
            var source = NewVisibleRun("in-room-" + scenario);
            var room = await EnterEventAsync<TabletOfTruth>(source);
            RunState variant = source.CloneExact();
            HiddenFutureCounterfactual.Change(variant);
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
        if (scenario is "hidden-preroll" or "sphere-late-1" or "sphere-late-2")
        {
            var source = NewVisibleRun("in-room-hidden-preroll");
            source.Players[0].Gold = 500;
            var room = await EnterEventAsync<CrystalSphere>(source);
            await room.Event.ChooseOption(room.Event.CurrentOptions[1]);
            var original = Assert.IsType<CrystalSphere>(room.Event);
            var game = original.Game!;
            var potion = game.Items.First(i => i.Kind == CrystalSphereItemKind.Potion && i.Position.HasValue);
            var card = game.Items.First(i => i.Kind == CrystalSphereItemKind.CardReward && i.Position.HasValue);
            if (scenario != "hidden-preroll")
            {
                foreach (var gold in game.Items.Where(i => i.Kind == CrystalSphereItemKind.Gold &&
                    !i.IsBigGold && i.Position.HasValue).Take(2).ToArray())
                    await original.RevealAsync(gold.Position!.Value.X, gold.Position.Value.Y, big: false);
                await original.RevealAsync(potion.Position!.Value.X, potion.Position.Value.Y + 1);
                await original.RevealAsync(card.Position!.Value.X, card.Position.Value.Y);
                if (scenario == "sphere-late-1") await original.RevealAsync(0, 0, big: false);
                Assert.Equal(scenario == "sphere-late-1" ? 1 : 2, game.DivinationCount);
                Assert.True(game.RevealedItems.Count >= 3, $"{scenario}: late board must have multiple revealed items");
            }
            RunState variant = source.CloneExact();

            var variantGame = Assert.IsType<CrystalSphere>(Assert.IsType<EventRoom>(variant.CurrentRoom).Event).Game!;
            // Layout is the #563 exception; only old RNG/hidden future may vary here.
            ((Rng)typeof(CrystalSphereMinigame).GetField("_rng", PrivateFields)!.GetValue(variantGame)!).NextInt(97);
            HiddenFutureCounterfactual.Change(variant);
            var before = RunCloneGraphSnapshot.Capture(source);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            RunState first = source.CloneReseeded(91);
            timer.Stop();
            Console.WriteLine($"#563 {scenario}: CloneReseeded {timer.Elapsed.TotalMilliseconds:F3} ms");
            Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(100),
                $"{scenario}: one clone took {timer.Elapsed.TotalMilliseconds:F3} ms");
            RunState second = variant.CloneReseeded(91);
            RunState different = source.CloneReseeded(92);
            var a = Assert.IsType<CrystalSphere>(Assert.IsType<EventRoom>(first.CurrentRoom).Event);
            var b = Assert.IsType<CrystalSphere>(Assert.IsType<EventRoom>(second.CurrentRoom).Event);
            var c = Assert.IsType<CrystalSphere>(Assert.IsType<EventRoom>(different.CurrentRoom).Event);
            Assert.Equal(original.UncoverFutureCost, a.UncoverFutureCost);
            foreach (var clone in new[] { a, b, c })
            {
                Assert.Equal(game.DivinationCount, clone.Game!.DivinationCount);
                Assert.Equal(game.PlacedAllItems, clone.Game.PlacedAllItems);
                Assert.Equal(game.Items.Select(SphereItemState), clone.Game.Items.Select(SphereItemState));
                Assert.Equal(game.RevealedItems.Select(SphereItemState), clone.Game.RevealedItems.Select(SphereItemState));
                for (int x = 0; x < 11; x++) for (int y = 0; y < 11; y++)
                    Assert.Equal(game.IsHidden(x, y), clone.Game.IsHidden(x, y));
                for (int i = 0; i < game.Items.Count; i++) Assert.NotSame(game.Items[i], clone.Game.Items[i]);
                Assert.Empty(clone.Game.Rewards);
            }
            // Fully uncover the same potion/card and finish at the same cell in every world.
            var outcomes = new List<string>();
            foreach (var ev in new[] { a, b, c, original })
            {
                if (ReferenceEquals(ev, original)) Assert.Equal(before, RunCloneGraphSnapshot.Capture(source));
                var current = ev.Game!;
                var rng = (Rng)typeof(CrystalSphereMinigame).GetField("_rng", PrivateFields)!.GetValue(current)!;
                var probe = rng.CloneExact();
                int counter = rng.Counter;
                if (scenario == "hidden-preroll")
                {
                    await ev.RevealAsync(potion.Position!.Value.X, potion.Position.Value.Y + 1);
                    await ev.RevealAsync(card.Position!.Value.X, card.Position.Value.Y);
                    Assert.Empty(current.Rewards);
                    Assert.Equal(counter, rng.Counter);
                }
                while (current.DivinationCount > 1) await ev.RevealAsync(0, 0, big: false);
                await ev.RevealAsync(0, 0, big: false);
                var expectedPotions = current.RevealedItems.Where(i => i.Kind == CrystalSphereItemKind.Potion)
                    .Select(i => probe.NextItem(Sts2Sim.Core.Factories.PotionFactory.GetOutOfCombatPool(ev.Owner)
                        .Where(p => p.Rarity == i.PotionRarity))!.Id).ToArray();
                Assert.NotEmpty(expectedPotions);
                Assert.Equal(expectedPotions, current.Rewards.OfType<PotionReward>().Select(r => r.Potion!.Id));
                outcomes.Add(string.Join(";", current.Rewards.Select(r => r switch
                {
                    PotionReward p => "potion:" + p.Potion!.Id,
                    CardReward reward => "cards:" + string.Join(",", reward.Options.Select(o => (o.Id, o.IsUpgraded))),
                    RelicReward relic => "relic:" + relic.Relic!.Id,
                    GoldReward gold => "gold:" + gold.Amount,
                    _ => r.GetType().Name,
                })));
            }
            Assert.Equal(outcomes[0], outcomes[1]); // Same new seed ignores old RNG/future.
            Assert.NotEqual(outcomes[0], outcomes[2]); // Different new seeds can change actual content.
            Assert.NotEqual(outcomes[0], outcomes[3]); // Source result is not copied.
            // Only the explicit source continuation above may mutate the source world.
            Assert.NotEqual(before, RunCloneGraphSnapshot.Capture(source));
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
            typeof(StandardActMap), typeof(SpoilsActMap), typeof(GoldenPathActMap), typeof(RunState.ActEntryMapState), typeof(MapPoint),
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
        string sourceBefore = RunCloneGraphSnapshot.Capture(source);
        RunState variant = source.CloneExact();
        EventRoom variantRoom = Assert.IsType<EventRoom>(variant.CurrentRoom);
        // Keep this assertion closure out of the alternate encounter factory below.
        {
            var exactPrepared = Assert.IsType<CombatRoom>(typeof(EventRoom)
                .GetField("_preparedCombatRoom", PrivateFields)!.GetValue(variantRoom));
            Assert.Null(exactPrepared.Engine);
            var selector = new FirstChoiceDecisionSource();
            exactPrepared.ConfigureCardSelectionSource(selector);
            Assert.Equal("A card selection source has already been configured.",
                Assert.Throws<InvalidOperationException>(() => exactPrepared.ConfigureCardSelectionSource(selector)).Message);
            Assert.Null(exactPrepared.Engine);
        }
        Assert.Equal(sourceBefore, RunCloneGraphSnapshot.Capture(source));
        EncounterDefinition encounter = sourceRoom.Event.CanonicalEncounter!;
        var alternate = new CombatRoom(() => (encounter, encounter.CreateMonsters(new Rng(0xBEEFUL))), RoomType.Monster);
        alternate.Prepare(variant);
        typeof(EventRoom).GetField("_preparedCombatRoom", PrivateFields)!.SetValue(variantRoom, alternate);
        HiddenFutureCounterfactual.Change(variant);
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
        // A quest owner can also reach a stable combat graph; each model must have one clone identity.
        var combat = new CombatState(run);
        combat.AddPlayerCreature(player.Creature);
        var enemy = combat.AddMonster(
            (MonsterModel)ModelDb.Get(typeof(TwigSlimeM)).MutableClone(), CombatSide.Enemy);
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
        var childCombat = Assert.IsType<CombatState>(child.Players[0].Creature.CombatState);
        var siblingCombat = Assert.IsType<CombatState>(sibling.Players[0].Creature.CombatState);
        var childEnemy = Assert.Single(childCombat.Enemies);
        var siblingEnemy = Assert.Single(siblingCombat.Enemies);
        Assert.NotSame(combat, childCombat);
        Assert.NotSame(childCombat, siblingCombat);
        Assert.NotSame(enemy.Monster, childEnemy.Monster);
        Assert.NotSame(childEnemy.Monster, siblingEnemy.Monster);
        Assert.Same(childCombat, childEnemy.CombatState);
        int originalHp = enemy.CurrentHp;
        childEnemy.LoseHpInternal(1m, Sts2Sim.Core.ValueProps.ValueProp.Unblockable);
        Assert.Equal(originalHp - 1, childEnemy.CurrentHp);
        Assert.Equal(originalHp, enemy.CurrentHp);
        Assert.Equal(originalHp, siblingEnemy.CurrentHp);
        RunState imagination = run.CloneReseeded(0x444UL);
        var imaginedCombat = Assert.IsType<CombatState>(imagination.Players[0].Creature.CombatState);
        var imaginedEnemy = Assert.Single(imaginedCombat.Enemies);
        Assert.NotSame(enemy, imaginedEnemy);
        Assert.NotSame(childEnemy, imaginedEnemy);
        Assert.Same(imaginedCombat, imaginedEnemy.CombatState);
        var imaginedQuest = imagination.Players[0].Deck.Cards.Single(card => card.GetType() == quest.GetType());
        Assert.Same(imaginedQuest, Assert.Single(imagination.Map.StartingMapPoint.Quests));
        Assert.Same(imaginedQuest, Assert.Single(imagination.Map.BossMapPoint.Quests));
        child.Map.StartingMapPoint.RemoveQuest(childQuest);
        Assert.Empty(child.Map.StartingMapPoint.Quests);
        Assert.Same(childQuest, Assert.Single(child.Map.BossMapPoint.Quests));
        Assert.Same(quest, Assert.Single(sourceStart.Quests));
        Assert.Same(siblingQuest, Assert.Single(sibling.Map.StartingMapPoint.Quests));
        sourceBoss.RemoveQuest(quest);
        Assert.Empty(sourceBoss.Quests);
        Assert.Same(childQuest, Assert.Single(child.Map.BossMapPoint.Quests));
        Assert.Same(siblingQuest, Assert.Single(sibling.Map.BossMapPoint.Quests));
        sourceStart.RemoveQuest(quest);
        CardModel canonicalQuest = ModelDb.Card<LanternKey>();
        sourceStart.AddQuest(canonicalQuest);
        Assert.Throws<InvalidOperationException>(() => run.CloneExact());
        Assert.Throws<InvalidOperationException>(() => run.CloneReseeded(0x444UL));
        Assert.False(canonicalQuest.HasOwner);
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
