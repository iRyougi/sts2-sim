using Sts2Sim.Core.Content;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Content;

public sealed class ActDefinitionBossPickTests
{
    [Fact]
    public void PickEncounter_BossLongRunCanReachAllThreeCandidates()
    {
        var act = new BossTestActDefinition();
        var rng = new Rng(seed: 7);

        var seen = new HashSet<EncounterDefinition>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < 100; i++)
        {
            seen.Add(act.PickEncounter(RoomType.Boss, rng));
        }

        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void PickEncounter_BossIsDeterministicForFreshActsWithTheSameRng()
    {
        var first = new BossTestActDefinition();
        var second = new BossTestActDefinition();
        var firstRng = new Rng(seed: 91);
        var secondRng = new Rng(seed: 91);

        string[] firstSequence = Enumerable.Range(0, 30)
            .Select(_ => first.GetBossLabel(first.PickEncounter(RoomType.Boss, firstRng)))
            .ToArray();
        string[] secondSequence = Enumerable.Range(0, 30)
            .Select(_ => second.GetBossLabel(second.PickEncounter(RoomType.Boss, secondRng)))
            .ToArray();

        Assert.Equal(firstSequence, secondSequence);
        Assert.Equal(firstRng.Counter, secondRng.Counter);
    }

    [Fact]
    public void PickEncounter_BossDoesNotConsumeOrPolluteTheMonsterBag()
    {
        var subject = new BossTestActDefinition();
        var control = new BossTestActDefinition();
        var subjectMonsterRng = new Rng(seed: 55);
        var controlMonsterRng = new Rng(seed: 55);

        Assert.Equal(
            control.GetMonsterLabel(control.PickEncounter(RoomType.Monster, controlMonsterRng)),
            subject.GetMonsterLabel(subject.PickEncounter(RoomType.Monster, subjectMonsterRng)));

        var bossRng = new Rng(seed: 144);
        for (int i = 0; i < 40; i++)
        {
            _ = subject.PickEncounter(RoomType.Boss, bossRng);
        }

        string[] subjectRemainder = Enumerable.Range(0, 5)
            .Select(_ => subject.GetMonsterLabel(
                subject.PickEncounter(RoomType.Monster, subjectMonsterRng)))
            .ToArray();
        string[] controlRemainder = Enumerable.Range(0, 5)
            .Select(_ => control.GetMonsterLabel(
                control.PickEncounter(RoomType.Monster, controlMonsterRng)))
            .ToArray();

        Assert.Equal(controlRemainder, subjectRemainder);
        Assert.Equal(controlMonsterRng.Counter, subjectMonsterRng.Counter);
    }

    private sealed class BossTestActDefinition : ActDefinition
    {
        public override int Index => 0;
        public override IReadOnlyList<Type> EventPool => Array.Empty<Type>();
        public override IReadOnlyList<Type> AncientPool => [typeof(Sts2Sim.Core.Models.Events.Neow)];
        private readonly IReadOnlyList<EncounterDefinition> _monsterEncounters =
            CreateEncounters(EncounterTag.Mushroom, EncounterTag.Crawler, EncounterTag.Shrinker);
        private readonly IReadOnlyList<EncounterDefinition> _eliteEncounters =
            CreateEncounters(EncounterTag.Nibbit, EncounterTag.Jaxfruit, EncounterTag.Slimes);
        private readonly IReadOnlyList<EncounterDefinition> _bossEncounters =
            CreateEncounters(EncounterTag.Mushroom, EncounterTag.Mushroom, EncounterTag.Mushroom);

        public override int BaseNumberOfRooms => 15;

        public override int NumberOfWeakEncounters => 0;

        protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => _monsterEncounters;

        protected override IReadOnlyList<EncounterDefinition> EliteEncounters => _eliteEncounters;

        protected override IReadOnlyList<EncounterDefinition> BossEncounters => _bossEncounters;

        public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);

        public string GetMonsterLabel(EncounterDefinition encounter) =>
            GetLabel(_monsterEncounters, encounter, "monster");

        public string GetBossLabel(EncounterDefinition encounter) =>
            GetLabel(_bossEncounters, encounter, "boss");

        private static string GetLabel(
            IReadOnlyList<EncounterDefinition> pool,
            EncounterDefinition encounter,
            string prefix)
        {
            int index = Enumerable.Range(0, pool.Count)
                .Single(candidate => ReferenceEquals(pool[candidate], encounter));
            return $"{prefix}-{index}";
        }

        private static IReadOnlyList<EncounterDefinition> CreateEncounters(params EncounterTag[] tags) =>
            tags.Select(tag => new EncounterDefinition(
                    (Func<MonsterModel>)(() =>
                        throw new NotSupportedException("This selection test does not create monsters.")),
                    new[] { tag }))
                .ToArray();
    }
}
