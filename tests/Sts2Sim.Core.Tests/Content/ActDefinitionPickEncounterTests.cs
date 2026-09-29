using Sts2Sim.Core.Content;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Content;

public sealed class ActDefinitionPickEncounterTests
{
    [Theory]
    [InlineData(RoomType.Monster)]
    [InlineData(RoomType.Elite)]
    public void PickEncounter_UsesAPersistentBagForEachNonBossPool(RoomType roomType)
    {
        var act = new TestActDefinition();
        var rng = new Rng(seed: 17);

        EncounterDefinition[] firstRound = Pick(act, roomType, rng, count: 3);

        Assert.Equal(3, firstRound.Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    [Fact]
    public void PickEncounter_DoesNotRepeatAnEncounterWithinABagRound()
    {
        for (ulong seed = 0; seed < 25; seed++)
        {
            var act = new TestActDefinition();
            var rng = new Rng(seed);

            for (int round = 0; round < 5; round++)
            {
                EncounterDefinition[] picks = Pick(act, RoomType.Monster, rng, count: 3);
                Assert.Equal(3, picks.Distinct(ReferenceEqualityComparer.Instance).Count());
            }
        }
    }

    [Fact]
    public void PickEncounter_FiltersSharedNonNoneTagsWhenAnAlternativeRemains()
    {
        for (ulong seed = 0; seed < 50; seed++)
        {
            var act = TestActDefinition.WithMonsterTags(
                new[] { EncounterTag.Mushroom },
                new[] { EncounterTag.Mushroom },
                new[] { EncounterTag.Crawler });
            var rng = new Rng(seed);

            EncounterDefinition first = act.PickEncounter(RoomType.Monster, rng);
            EncounterDefinition second = act.PickEncounter(RoomType.Monster, rng);

            if (!ReferenceEquals(first, act.MonsterEncountersForTests[2]))
            {
                Assert.Same(act.MonsterEncountersForTests[2], second);
            }
        }
    }

    [Fact]
    public void PickEncounter_WhenTagPredicateHasNoMatch_FallsBackToRemainingEntry()
    {
        for (ulong seed = 0; seed < 25; seed++)
        {
            var act = TestActDefinition.WithMonsterTags(
                new[] { EncounterTag.Slimes },
                new[] { EncounterTag.Slimes });
            var rng = new Rng(seed);

            EncounterDefinition first = act.PickEncounter(RoomType.Monster, rng);
            EncounterDefinition second = act.PickEncounter(RoomType.Monster, rng);

            Assert.NotSame(first, second);
        }
    }

    [Fact]
    public void PickEncounter_RefillPreservesPreviousEncounterAndTagConstraint()
    {
        for (ulong seed = 0; seed < 100; seed++)
        {
            var act = TestActDefinition.WithMonsterTags(
                new[] { EncounterTag.Mushroom },
                new[] { EncounterTag.Crawler });
            var rng = new Rng(seed);

            _ = act.PickEncounter(RoomType.Monster, rng);
            EncounterDefinition endOfFirstRound = act.PickEncounter(RoomType.Monster, rng);
            EncounterDefinition startOfSecondRound = act.PickEncounter(RoomType.Monster, rng);

            Assert.NotSame(endOfFirstRound, startOfSecondRound);
            Assert.DoesNotContain(
                startOfSecondRound.Tags,
                endOfFirstRound.Tags.Contains);
        }
    }

    [Fact]
    public void PickEncounter_WeakToRegularBoundaryPreservesLastTagConstraint()
    {
        var act = new WeakBoundaryActDefinition();
        var rng = new Rng(19);

        EncounterDefinition weak = act.PickEncounter(RoomType.Monster, rng);
        EncounterDefinition regular = act.PickEncounter(RoomType.Monster, rng);

        Assert.True(weak.IsWeak);
        Assert.Equal([EncounterTag.Mushroom], weak.Tags);
        Assert.False(regular.IsWeak);
        Assert.Equal([EncounterTag.Crawler], regular.Tags);
    }

    [Fact]
    public void PickEncounter_MonsterAndEliteBagStateAreIndependent()
    {
        for (ulong seed = 0; seed < 25; seed++)
        {
            var interleaved = new TestActDefinition();
            var isolated = new TestActDefinition();
            var interleavedMonsterRng = new Rng(seed);
            var interleavedEliteRng = new Rng(seed + 100);
            var isolatedMonsterRng = new Rng(seed);
            var isolatedEliteRng = new Rng(seed + 100);
            var interleavedMonsters = new List<string>();
            var interleavedElites = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                interleavedMonsters.Add(interleaved.PickLabel(RoomType.Monster, interleavedMonsterRng));
                interleavedElites.Add(interleaved.PickLabel(RoomType.Elite, interleavedEliteRng));
            }

            string[] isolatedMonsters = Enumerable.Range(0, 3)
                .Select(_ => isolated.PickLabel(RoomType.Monster, isolatedMonsterRng))
                .ToArray();
            string[] isolatedElites = Enumerable.Range(0, 3)
                .Select(_ => isolated.PickLabel(RoomType.Elite, isolatedEliteRng))
                .ToArray();

            Assert.Equal(isolatedMonsters, interleavedMonsters);
            Assert.Equal(isolatedElites, interleavedElites);
        }
    }

    [Fact]
    public void PickEncounter_FreshActInstancesWithTheSameRngProduceTheSameSequence()
    {
        var first = new TestActDefinition();
        var second = new TestActDefinition();
        var firstRng = new Rng(seed: 42);
        var secondRng = new Rng(seed: 42);

        string[] firstSequence = Enumerable.Range(0, 30)
            .Select(_ => first.PickLabel(RoomType.Monster, firstRng))
            .ToArray();
        string[] secondSequence = Enumerable.Range(0, 30)
            .Select(_ => second.PickLabel(RoomType.Monster, secondRng))
            .ToArray();

        Assert.Equal(firstSequence, secondSequence);
        Assert.Equal(firstRng.Counter, secondRng.Counter);
    }

    [Fact]
    public void PickEncounter_DifferentRunActInstancesDoNotLeakBagState()
    {
        var disturbedAct = new TestActDefinition();
        var subjectAct = new TestActDefinition();
        var controlAct = new TestActDefinition();
        var disturbedRun = new RunState("act-bag-isolation", disturbedAct);
        var subjectRun = new RunState("act-bag-isolation", subjectAct);
        var controlRun = new RunState("act-bag-isolation", controlAct);

        _ = Pick(disturbedRun.Act, RoomType.Monster, disturbedRun.Rng.CombatCardGeneration, count: 7);
        string[] subjectSequence = Enumerable.Range(0, 6)
            .Select(_ => subjectAct.PickLabel(
                RoomType.Monster,
                subjectRun.Rng.CombatCardGeneration))
            .ToArray();
        string[] controlSequence = Enumerable.Range(0, 6)
            .Select(_ => controlAct.PickLabel(
                RoomType.Monster,
                controlRun.Rng.CombatCardGeneration))
            .ToArray();

        Assert.NotSame(disturbedRun.Act, subjectRun.Act);
        Assert.NotSame(subjectRun.Act, controlRun.Act);
        Assert.Equal(controlSequence, subjectSequence);
    }

    [Fact]
    public void PickEncounter_UnknownRoomTypeThrows()
    {
        var act = new TestActDefinition();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => act.PickEncounter(RoomType.Shop, new Rng(seed: 1)));

        Assert.Equal("roomType", exception.ParamName);
        Assert.Equal(RoomType.Shop, exception.ActualValue);
    }

    private static EncounterDefinition[] Pick(
        ActDefinition act,
        RoomType roomType,
        Rng rng,
        int count) => Enumerable.Range(0, count)
            .Select(_ => act.PickEncounter(roomType, rng))
            .ToArray();

    private sealed class WeakBoundaryActDefinition : ActDefinition
    {
        public override int Index => 0;
        public override IReadOnlyList<Type> EventPool => Array.Empty<Type>();
        public override IReadOnlyList<Type> AncientPool => [typeof(Sts2Sim.Core.Models.Events.Neow)];
        private static EncounterDefinition Create(EncounterTag tag, bool isWeak = false) =>
            new(
                (Func<MonsterModel>)(() =>
                    throw new NotSupportedException("This selection test does not create monsters.")),
                [tag],
                isWeak);

        private readonly IReadOnlyList<EncounterDefinition> _monsters =
        [
            Create(EncounterTag.Mushroom, isWeak: true),
            Create(EncounterTag.Mushroom),
            Create(EncounterTag.Crawler),
        ];
        private readonly IReadOnlyList<EncounterDefinition> _nonMonster =
            [Create(EncounterTag.None)];

        public override int BaseNumberOfRooms => 15;
        public override int NumberOfWeakEncounters => 1;
        protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => _monsters;
        protected override IReadOnlyList<EncounterDefinition> EliteEncounters => _nonMonster;
        protected override IReadOnlyList<EncounterDefinition> BossEncounters => _nonMonster;
        public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);
    }
    private sealed class TestActDefinition : ActDefinition
    {
        public override int Index => 0;
        public override IReadOnlyList<Type> EventPool => Array.Empty<Type>();
        public override IReadOnlyList<Type> AncientPool => [typeof(Sts2Sim.Core.Models.Events.Neow)];
        private readonly IReadOnlyList<EncounterDefinition> _monsterEncounters;
        private readonly IReadOnlyList<EncounterDefinition> _eliteEncounters;
        private readonly IReadOnlyList<EncounterDefinition> _bossEncounters;

        public IReadOnlyList<EncounterDefinition> MonsterEncountersForTests => _monsterEncounters;

        public override int BaseNumberOfRooms => 15;

        public override int NumberOfWeakEncounters => 0;

        protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => _monsterEncounters;

        protected override IReadOnlyList<EncounterDefinition> EliteEncounters => _eliteEncounters;

        protected override IReadOnlyList<EncounterDefinition> BossEncounters => _bossEncounters;

        public TestActDefinition()
            : this(
                new[]
                {
                    new[] { EncounterTag.Mushroom },
                    new[] { EncounterTag.Crawler },
                    new[] { EncounterTag.Shrinker },
                })
        {
        }

        private TestActDefinition(IReadOnlyList<IReadOnlyList<EncounterTag>> monsterTags)
        {
            _monsterEncounters = monsterTags.Select(CreateEncounter).ToArray();
            _eliteEncounters = new[]
            {
                CreateEncounter(new[] { EncounterTag.Nibbit }),
                CreateEncounter(new[] { EncounterTag.Jaxfruit }),
                CreateEncounter(new[] { EncounterTag.Slimes }),
            };
            _bossEncounters = new[] { CreateEncounter(new[] { EncounterTag.None }) };
        }

        public static TestActDefinition WithMonsterTags(params IReadOnlyList<EncounterTag>[] tags) => new(tags);

        public string PickLabel(RoomType roomType, Rng rng)
        {
            EncounterDefinition encounter = PickEncounter(roomType, rng);
            IReadOnlyList<EncounterDefinition> pool = roomType switch
            {
                RoomType.Monster => _monsterEncounters,
                RoomType.Elite => _eliteEncounters,
                _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, null),
            };
            int index = Enumerable.Range(0, pool.Count)
                .Single(candidate => ReferenceEquals(pool[candidate], encounter));
            return $"{roomType}-{index}";
        }

        public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);

        private static EncounterDefinition CreateEncounter(IReadOnlyList<EncounterTag> tags) =>
            new(
                (Func<MonsterModel>)(() =>
                    throw new NotSupportedException("This selection test does not create monsters.")),
                tags);
    }
}
