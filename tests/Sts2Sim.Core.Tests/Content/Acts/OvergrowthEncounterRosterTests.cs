using System.Reflection;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Content.Acts;

[Collection("ModelDb")]
public sealed class OvergrowthEncounterRosterTests : IDisposable
{
    public OvergrowthEncounterRosterTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Roster_HasAll22RealEncountersInAuthoritativeCategories_WithExactTagsAndNoPlaceholders()
    {
        var act = new Overgrowth();
        IReadOnlyList<EncounterDefinition> monsters = GetPool(act, "MonsterEncounters");
        IReadOnlyList<EncounterDefinition> elites = GetPool(act, "EliteEncounters");
        IReadOnlyList<EncounterDefinition> bosses = GetPool(act, "BossEncounters");

        Assert.Equal(16, monsters.Count);
        Assert.Equal(3, elites.Count);
        Assert.Equal(3, bosses.Count);
        Assert.Equal(22, monsters.Count + elites.Count + bosses.Count);

        EncounterTag[][] expectedMonsterTags =
        [
            [],
            [EncounterTag.Mushroom, EncounterTag.Slimes],
            [],
            [EncounterTag.Crawler],
            [],
            [],
            [],
            [EncounterTag.Nibbit],
            [EncounterTag.Shrinker, EncounterTag.Crawler],
            [],
            [EncounterTag.Shrinker],
            [EncounterTag.Slimes],
            [EncounterTag.Slimes],
            [EncounterTag.Jaxfruit, EncounterTag.Slimes],
            [EncounterTag.Mushroom, EncounterTag.Jaxfruit],
            [],
        ];
        Assert.Equal(expectedMonsterTags.Length, monsters.Count);
        for (int i = 0; i < monsters.Count; i++)
        {
            Assert.Equal(expectedMonsterTags[i], monsters[i].Tags);
        }
        Assert.Equal(
            [false, false, false, true, false, false, false, true, false, false, true, false, true, false, false, false],
            monsters.Select(encounter => encounter.IsWeak));
        Assert.All(elites, encounter => Assert.Empty(encounter.Tags));
        Assert.All(bosses, encounter => Assert.Empty(encounter.Tags));

        AssertBatch(monsters[0].CreateMonsters(new Rng(0)), [typeof(CubexConstruct)], [null]);
        AssertBatch(monsters[3].CreateMonsters(new Rng(0)), [typeof(FuzzyWurmCrawler)], [null]);
        AssertBatch(monsters[5].CreateMonsters(new Rng(0)), [typeof(Mawler)], [null]);
        AssertBatch(monsters[10].CreateMonsters(new Rng(0)), [typeof(ShrinkerBeetle)], [null]);
        AssertBatch(monsters[14].CreateMonsters(new Rng(0)), [typeof(SnappingJaxfruit), typeof(Flyconid)], [null, null]);
        AssertBatch(monsters[15].CreateMonsters(new Rng(0)), [typeof(VineShambler)], [null]);

        AssertBatch(elites[0].CreateMonsters(new Rng(0)), [typeof(BygoneEffigy)], [null]);
        AssertBatch(elites[1].CreateMonsters(new Rng(0)), [typeof(Byrdonis)], [null]);
        AssertBatch(elites[2].CreateMonsters(new Rng(0)), [typeof(PhrogParasite)], ["phrog"]);
        AssertBatch(bosses[0].CreateMonsters(new Rng(0)), [typeof(CeremonialBeast)], [null]);
        AssertBatch(bosses[2].CreateMonsters(new Rng(0)), [typeof(Vantom)], [null]);

        Type[] placeholders = [typeof(WanderingGrunt), typeof(HardyBrute), typeof(ActOneGuardian)];
        IEnumerable<Type> generatedTypes = monsters.Concat(elites).Concat(bosses)
            .SelectMany(encounter => encounter.CreateMonsters(new Rng(17)))
            .Select(entry => entry.Monster.GetType());
        Assert.DoesNotContain(generatedTypes, placeholders.Contains);
    }

    [Fact]
    public void FixedCompositions_PreserveAuthoritativeOrderSlotsAndPerInstanceFlags()
    {
        var act = new Overgrowth();
        IReadOnlyList<EncounterDefinition> monsters = GetPool(act, "MonsterEncounters");
        IReadOnlyList<EncounterDefinition> elites = GetPool(act, "EliteEncounters");
        IReadOnlyList<EncounterDefinition> bosses = GetPool(act, "BossEncounters");

        AssertBatch(monsters[8].CreateMonsters(new Rng(0)),
            [typeof(ShrinkerBeetle), typeof(FuzzyWurmCrawler)], [null, null]);

        var nibbits = monsters[6].CreateMonsters(new Rng(0));
        AssertBatch(nibbits, [typeof(Nibbit), typeof(Nibbit)], ["front", "back"]);
        Assert.True(Assert.IsType<Nibbit>(nibbits[0].Monster).IsFront);
        Assert.False(Assert.IsType<Nibbit>(nibbits[1].Monster).IsFront);
        var nibbitsAgain = monsters[6].CreateMonsters(new Rng(0));
        Assert.NotSame(nibbits[0].Monster, nibbitsAgain[0].Monster);
        Assert.False(ModelDb.Monster<Nibbit>().IsFront);
        Assert.False(ModelDb.Monster<Nibbit>().IsAlone);

        var nibbitWeak = Assert.Single(monsters[7].CreateMonsters(new Rng(0)));
        Assert.Null(nibbitWeak.SlotName);
        Assert.True(Assert.IsType<Nibbit>(nibbitWeak.Monster).IsAlone);

        var inklets = monsters[4].CreateMonsters(new Rng(0));
        AssertBatch(inklets, [typeof(Inklet), typeof(Inklet), typeof(Inklet)], [null, null, null]);
        Assert.Equal([false, true, false], inklets.Select(entry => Assert.IsType<Inklet>(entry.Monster).MiddleInklet));
        Assert.False(ModelDb.Monster<Inklet>().MiddleInklet);

        AssertBatch(monsters[2].CreateMonsters(new Rng(0)), [typeof(Fogmog)], ["fogmog"]);
        AssertBatch(elites[2].CreateMonsters(new Rng(0)), [typeof(PhrogParasite)], ["phrog"]);

        var kin = bosses[1].CreateMonsters(new Rng(0));
        AssertBatch(kin, [typeof(KinFollower), typeof(KinFollower), typeof(KinPriest)], ["slot1", "slot2", "leaderSlot"]);
        Assert.True(Assert.IsType<KinFollower>(kin[0].Monster).StartsWithDance);
        Assert.False(Assert.IsType<KinFollower>(kin[1].Monster).StartsWithDance);
        Assert.False(ModelDb.Monster<KinFollower>().StartsWithDance);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(19UL)]
    public void RandomizedCompositions_ConsumeExactDrawsAndPreserveCandidateOrdering(ulong seed)
    {
        var act = new Overgrowth();
        IReadOnlyList<EncounterDefinition> monsters = GetPool(act, "MonsterEncounters");

        AssertFlyconid(monsters[1], seed);
        AssertRubyRaiders(monsters[9], seed);
        AssertSlimesNormal(monsters[11], seed);
        AssertSlimesWeak(monsters[12], seed);
    }

    [Fact]
    public void SlitheringStrangler_AllBranchesUseExactExtraDrawCountsAndCandidateOrder()
    {
        EncounterDefinition encounter = GetPool(new Overgrowth(), "MonsterEncounters")[13];

        for (int branch = 0; branch < 3; branch++)
        {
            ulong seed = FindSeedForFirstIndex(branch, 3);
            var expectedRng = new Rng(seed);
            Assert.Equal(branch, expectedRng.NextInt(0, 3));
            var expected = new List<Type>();
            if (branch == 0)
            {
                expected.Add(typeof(SnappingJaxfruit));
            }
            else if (branch == 1)
            {
                Type[] medium = [typeof(LeafSlimeM), typeof(TwigSlimeM)];
                expected.Add(medium[expectedRng.NextInt(0, medium.Length)]);
            }
            else
            {
                Type[] small = [typeof(LeafSlimeS), typeof(TwigSlimeS)];
                expected.Add(small[expectedRng.NextInt(0, small.Length)]);
                expected.Add(small[expectedRng.NextInt(0, small.Length)]);
            }
            expected.Add(typeof(SlitheringStrangler));

            var actualRng = new Rng(seed);
            IReadOnlyList<(MonsterModel Monster, string? SlotName)> actual = encounter.CreateMonsters(actualRng);

            Assert.Equal(expected, actual.Select(entry => entry.Monster.GetType()));
            Assert.All(actual, entry => Assert.Null(entry.SlotName));
            Assert.Equal(expectedRng.Counter, actualRng.Counter);
        }
    }

    private static void AssertFlyconid(EncounterDefinition encounter, ulong seed)
    {
        Type[] candidates = [typeof(LeafSlimeM), typeof(TwigSlimeM)];
        var expectedRng = new Rng(seed);
        Type expectedSlime = candidates[expectedRng.NextInt(0, candidates.Length)];
        var actualRng = new Rng(seed);
        var actual = encounter.CreateMonsters(actualRng);
        AssertBatch(actual, [expectedSlime, typeof(Flyconid)], [null, null]);
        Assert.Equal(expectedRng.Counter, actualRng.Counter);
    }

    private static void AssertRubyRaiders(EncounterDefinition encounter, ulong seed)
    {
        var candidates = new List<Type>
        {
            typeof(AxeRubyRaider), typeof(AssassinRubyRaider), typeof(BruteRubyRaider),
            typeof(CrossbowRubyRaider), typeof(TrackerRubyRaider),
        };
        var expectedRng = new Rng(seed);
        var expected = new List<Type>();
        foreach (int expectedCandidateCount in new[] { 5, 4, 3 })
        {
            Assert.Equal(expectedCandidateCount, candidates.Count);
            int index = expectedRng.NextInt(0, candidates.Count);
            expected.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
        var actualRng = new Rng(seed);
        var actual = encounter.CreateMonsters(actualRng);
        Assert.Equal(expected, actual.Select(entry => entry.Monster.GetType()));
        Assert.Equal(3, actual.Select(entry => entry.Monster.GetType()).Distinct().Count());
        Assert.Equal(expectedRng.Counter, actualRng.Counter);
    }

    private static void AssertSlimesNormal(EncounterDefinition encounter, ulong seed)
    {
        var expectedRng = new Rng(seed);
        bool leafFirst = expectedRng.NextBool();
        Type[] expected = leafFirst
            ? [typeof(TwigSlimeM), typeof(LeafSlimeM), typeof(LeafSlimeS), typeof(TwigSlimeS)]
            : [typeof(TwigSlimeM), typeof(LeafSlimeM), typeof(TwigSlimeS), typeof(LeafSlimeS)];
        var actualRng = new Rng(seed);
        var actual = encounter.CreateMonsters(actualRng);
        AssertBatch(actual, expected, [null, null, null, null]);
        Assert.Equal(expectedRng.Counter, actualRng.Counter);
    }

    private static void AssertSlimesWeak(EncounterDefinition encounter, ulong seed)
    {
        var small = new List<Type> { typeof(LeafSlimeS), typeof(TwigSlimeS) };
        Type[] medium = [typeof(LeafSlimeM), typeof(TwigSlimeM)];
        var expectedRng = new Rng(seed);
        int firstIndex = expectedRng.NextInt(0, small.Count);
        Type first = small[firstIndex];
        small.RemoveAt(firstIndex);
        Type second = small[expectedRng.NextInt(0, small.Count)];
        Type middle = medium[expectedRng.NextInt(0, medium.Length)];
        var actualRng = new Rng(seed);
        var actual = encounter.CreateMonsters(actualRng);
        AssertBatch(actual, [first, middle, second], [null, null, null]);
        Assert.Equal(3, actualRng.Counter);
        Assert.Equal(expectedRng.Counter, actualRng.Counter);
    }

    private static ulong FindSeedForFirstIndex(int target, int candidateCount)
    {
        for (ulong seed = 0; seed < 1000; seed++)
        {
            if (new Rng(seed).NextInt(0, candidateCount) == target)
            {
                return seed;
            }
        }
        throw new InvalidOperationException($"No seed found for target index {target}.");
    }

    private static IReadOnlyList<EncounterDefinition> GetPool(Overgrowth act, string propertyName)
    {
        PropertyInfo property = typeof(Overgrowth).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsAssignableFrom<IReadOnlyList<EncounterDefinition>>(property.GetValue(act));
    }

    private static void AssertBatch(
        IReadOnlyList<(MonsterModel Monster, string? SlotName)> actual,
        IReadOnlyList<Type> expectedTypes,
        IReadOnlyList<string?> expectedSlots)
    {
        Assert.Equal(expectedTypes, actual.Select(entry => entry.Monster.GetType()));
        Assert.Equal(expectedSlots, actual.Select(entry => entry.SlotName));
        Assert.All(actual, entry => Assert.True(entry.Monster.IsMutable));
    }
}
