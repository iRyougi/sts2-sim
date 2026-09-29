using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class NeowTests : IDisposable
{
    public NeowTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Archived8F9CPYQ6QYEN_NeowEntryEndsAt56Hp()
    {
        // run_history.json F1 records current_hp=56, hp_healed=56, max_hp=70,
        // damage_taken=0. These are archived observations, not a calculated expectation.
        var run = new RunState("8F9CPYQ6QYEN", ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        AbstractRoom room = RoomFactory.CreateAncientEventRoom(run);
        run.PushRoom(room);
        try
        {
            await room.Enter(run);

            Assert.IsType<Neow>(Assert.IsType<EventRoom>(room).Event);
            Assert.Equal(70, player.Creature.MaxHp);
            Assert.Equal(56, player.Creature.CurrentHp);
            Assert.Equal(0, player.Creature.CumulativeHpLost);
        }
        finally
        {
            await room.Exit(run);
            run.PopCurrentRoom();
        }
    }
    [Fact]
    public void AllPossibleOptions_ReturnsExactThirtyMutableClonesInAuthoritativeOrder()
    {
        var neow = (Neow)ModelDb.Event<Neow>().MutableClone();

        IReadOnlyList<RelicModel> first = neow.AllPossibleOptions;
        IReadOnlyList<RelicModel> second = neow.AllPossibleOptions;

        Assert.Equal(
            new[]
            {
                typeof(CursedPearl),
                typeof(DowsingRod),
                typeof(HeftyTablet),
                typeof(LargeCapsule),
                typeof(LeafyPoultice),
                typeof(NeowsBones),
                typeof(NeowsSacrifice),
                typeof(PrecariousShears),
                typeof(SilkenTress),
                typeof(SilverCrucible),
                typeof(ArcaneScroll),
                typeof(BoomingConch),
                typeof(FishingRod),
                typeof(GoldenPearl),
                typeof(Kaleidoscope),
                typeof(LeadPaperweight),
                typeof(LostCoffer),
                typeof(MassiveScroll),
                typeof(NeowsTorment),
                typeof(NewLeaf),
                typeof(PhialHolster),
                typeof(PreciseScissors),
                typeof(ScrollBoxes),
                typeof(WingedBoots),
                typeof(LavaRock),
                typeof(NeowsTalisman),
                typeof(NutritiousOyster),
                typeof(Pomander),
                typeof(SmallCapsule),
                typeof(StoneHumidifier),
            },
            first.Select(relic => relic.GetType()));
        Assert.Equal(30, first.Select(relic => relic.GetType()).Distinct().Count());
        Assert.All(first, relic =>
        {
            Assert.Equal(RelicRarity.Ancient, relic.Rarity);
            Assert.False(relic.IsCanonical);
        });
        Assert.All(
            first.Zip(second),
            pair => Assert.NotSame(pair.First, pair.Second));
    }

    [Fact]
    public void InitialOptions_AcrossDeterministicSeedsHonorOrderExclusionsBranchesFilteringAndRngCounts()
    {
        OptionSnapshot[] snapshots = Enumerable.Range(0, 512)
            .Select(index => Snapshot($"neow-options-{index}"))
            .ToArray();
        var curseNames = new HashSet<string>(
            new[]
            {
                nameof(CursedPearl),
                nameof(DowsingRod),
                nameof(HeftyTablet),
                nameof(LargeCapsule),
                nameof(LeafyPoultice),
                nameof(NeowsBones),
                nameof(NeowsSacrifice),
                nameof(PrecariousShears),
                nameof(SilkenTress),
                nameof(SilverCrucible),
            },
            StringComparer.Ordinal);

        Assert.All(snapshots, snapshot =>
        {
            Assert.Equal(3, snapshot.OptionKeys.Count);
            Assert.DoesNotContain(snapshot.OptionKeys[0], curseNames);
            Assert.DoesNotContain(snapshot.OptionKeys[1], curseNames);
            Assert.Contains(snapshot.OptionKeys[2], curseNames);
            Assert.DoesNotContain(nameof(MassiveScroll), snapshot.OptionKeys);
            Assert.Equal(ExpectedRngCounter(snapshot.OptionKeys[2]), snapshot.RngCounter);
        });
        Assert.Equal(curseNames, snapshots.Select(snapshot => snapshot.OptionKeys[2]).ToHashSet());

        AssertExcluded(snapshots, nameof(CursedPearl), nameof(GoldenPearl));
        AssertExcluded(snapshots, nameof(HeftyTablet), nameof(ArcaneScroll));
        AssertExcluded(snapshots, nameof(LeafyPoultice), nameof(NewLeaf));
        AssertExcluded(snapshots, nameof(PrecariousShears), nameof(PreciseScissors));
        AssertExcluded(
            snapshots,
            nameof(NeowsSacrifice),
            nameof(PhialHolster),
            nameof(LostCoffer));

        OptionSnapshot[] largeCapsule = snapshots
            .Where(snapshot => snapshot.OptionKeys[2] == nameof(LargeCapsule))
            .ToArray();
        Assert.NotEmpty(largeCapsule);
        Assert.All(largeCapsule, snapshot =>
        {
            Assert.DoesNotContain(nameof(LavaRock), snapshot.OptionKeys);
            Assert.DoesNotContain(nameof(SmallCapsule), snapshot.OptionKeys);
            Assert.Equal(17, snapshot.RngCounter);
        });

        AssertPairedBranchHasExclusiveOutcomes(snapshots, nameof(LavaRock), nameof(SmallCapsule));
        AssertPairedBranchHasExclusiveOutcomes(
            snapshots,
            nameof(NutritiousOyster),
            nameof(StoneHumidifier));
        AssertPairedBranchHasExclusiveOutcomes(snapshots, nameof(NeowsTalisman), nameof(Pomander));
    }

    [Fact]
    public async Task SelectingAnOptionObtainsOwnedMutableRelicAndFinishesEvent()
    {
        RunState runState = CreateRunState("neow-select");
        var room = new EventRoom(() => (Neow)ModelDb.Event<Neow>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        var neow = Assert.IsType<Neow>(room.Event);
        EventOption selected = neow.CurrentOptions[0];
        Type selectedType = neow.AllPossibleOptions
            .Select(relic => relic.GetType())
            .Single(type => type.Name == selected.Key);

        await neow.ChooseOption(selected);

        RelicModel obtained = Assert.Single(
            runState.Players[0].Relics,
            relic => relic.GetType() == selectedType);
        Assert.False(obtained.IsCanonical);
        Assert.Same(runState.Players[0], obtained.Owner);
        Assert.True(neow.IsFinished);
    }

    private static OptionSnapshot Snapshot(string seed)
    {
        RunState runState = CreateRunState(seed);
        Player player = runState.Players[0];
        var neow = (Neow)ModelDb.Event<Neow>().MutableClone();
        neow.AssignOwner(player);
        neow.BeginEvent(runState);
        return new OptionSnapshot(
            neow.CurrentOptions.Select(option => option.Key).ToArray(),
            neow.Rng.Counter);
    }

    private static RunState CreateRunState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }

    private static int ExpectedRngCounter(string curseName) => curseName switch
    {
        nameof(CursedPearl) or nameof(HeftyTablet) or nameof(LeafyPoultice) or
            nameof(PrecariousShears) => 18,
        nameof(NeowsSacrifice) or nameof(LargeCapsule) => 17,
        _ => 19,
    };

    private static void AssertExcluded(
        IEnumerable<OptionSnapshot> snapshots,
        string curseName,
        params string[] excludedPositiveNames)
    {
        OptionSnapshot[] matching = snapshots
            .Where(snapshot => snapshot.OptionKeys[2] == curseName)
            .ToArray();
        Assert.NotEmpty(matching);
        Assert.All(
            matching,
            snapshot => Assert.DoesNotContain(
                snapshot.OptionKeys,
                key => excludedPositiveNames.Contains(key, StringComparer.Ordinal)));
    }

    private static void AssertPairedBranchHasExclusiveOutcomes(
        IEnumerable<OptionSnapshot> snapshots,
        string first,
        string second)
    {
        OptionSnapshot[] materialized = snapshots.ToArray();
        Assert.All(
            materialized,
            snapshot => Assert.False(
                snapshot.OptionKeys.Contains(first, StringComparer.Ordinal) &&
                snapshot.OptionKeys.Contains(second, StringComparer.Ordinal)));
        Assert.Contains(materialized, snapshot => snapshot.OptionKeys.Contains(first, StringComparer.Ordinal));
        Assert.Contains(materialized, snapshot => snapshot.OptionKeys.Contains(second, StringComparer.Ordinal));
    }

    private sealed record OptionSnapshot(IReadOnlyList<string> OptionKeys, int RngCounter);
}
