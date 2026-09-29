namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class FlyconidTests : IDisposable
{
    private readonly Task14MonsterTestFixture _fixture = new(typeof(Flyconid));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 47, 49)]
    [InlineData((int)AscensionLevel.ToughEnemies, 51, 53)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (_, Flyconid monster, _) = Task14MonsterTestFixture.CreateCombat<Flyconid>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Fact]
    public async Task InitialAndRandBranches_HaveEqualWeightsAndRuntimeCooldownRules()
    {
        var seenInitial = new HashSet<string>();
        var seenRand = new HashSet<string>();

        for (int seed = 0; seed < 64; seed++)
        {
            (_, Flyconid monster, IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets) =
                Task14MonsterTestFixture.CreateCombat<Flyconid>(0, $"flyconid-{seed}");
            string previous = monster.NextMove!.StateId;
            seenInitial.Add(previous);
            var history = new List<string> { previous };
            Assert.Contains(previous, new[] { "FRAIL_SPORES_MOVE", "SMASH_MOVE" });
            Assert.NotEqual("VULNERABLE_SPORES_MOVE", previous);

            for (int turn = 0; turn < 18; turn++)
            {
                await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
                string current = monster.NextMove!.StateId;
                seenRand.Add(current);
                Assert.NotEqual(previous, current);
                int cooldown = current == "VULNERABLE_SPORES_MOVE" ? 3 : current == "FRAIL_SPORES_MOVE" ? 2 : 0;
                // Native RandomBranchState selects its first branch when all weights
                // are zero (NextFloat(0), then <= 0). Preserve that edge case.
                bool anyEligible = !history.TakeLast(3).Contains("VULNERABLE_SPORES_MOVE")
                    || !history.TakeLast(2).Contains("FRAIL_SPORES_MOVE")
                    || previous != "SMASH_MOVE";
                if (anyEligible)
                    Assert.DoesNotContain(current, history.TakeLast(cooldown));
                else
                    Assert.Equal("VULNERABLE_SPORES_MOVE", current);
                history.Add(current);
                previous = current;
            }
        }

        Assert.Equal(
            new[] { "FRAIL_SPORES_MOVE", "SMASH_MOVE" },
            seenInitial.OrderBy(id => id));
        Assert.Equal(
            new[] { "FRAIL_SPORES_MOVE", "SMASH_MOVE", "VULNERABLE_SPORES_MOVE" },
            seenRand.OrderBy(id => id));

        (_, Flyconid inspected, _) = Task14MonsterTestFixture.CreateCombat<Flyconid>(0, "flyconid-weights");
        RandomBranchState initial = Assert.IsType<RandomBranchState>(inspected.MoveStateMachine!.States["INITIAL"]);
        Assert.Collection(
            initial.States,
            state => AssertBranch(state, "FRAIL_SPORES_MOVE", 1f),
            state => AssertBranch(state, "SMASH_MOVE", 1f));
        RandomBranchState random = Assert.IsType<RandomBranchState>(inspected.MoveStateMachine.States["RAND"]);
        Assert.Collection(
            random.States,
            state => AssertBranch(state, "VULNERABLE_SPORES_MOVE", 1f),
            state => AssertBranch(state, "FRAIL_SPORES_MOVE", 1f),
            state => AssertBranch(state, "SMASH_MOVE", 1f));
    }

    [Theory]
    [InlineData(0, 11, 8)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 12, 9)]
    public async Task ThreeMoves_ExposeAuthoritativeIntentsAndTwoTargetEffects(
        int ascension,
        int expectedSmashDamage,
        int expectedSporeDamage)
    {
        (_, Flyconid monster, IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets) =
            Task14MonsterTestFixture.CreateCombat<Flyconid>(ascension, targetCount: 2);

        Task14MonsterTestFixture.ForceMove(monster, "VULNERABLE_SPORES_MOVE");
        Assert.IsType<DebuffIntent>(Assert.Single(monster.NextMove!.Intents));
        await monster.PerformMove();
        Assert.All(targets, target => Assert.Equal(2, Assert.IsType<VulnerablePower>(target.GetPower<VulnerablePower>()).Amount));

        (_, monster, targets) = Task14MonsterTestFixture.CreateCombat<Flyconid>(
            ascension,
            "flyconid-frail-effect",
            targetCount: 2);
        Task14MonsterTestFixture.ForceMove(monster, "FRAIL_SPORES_MOVE");
        Assert.Collection(
            monster.NextMove!.Intents,
            intent => Assert.Equal(expectedSporeDamage, Assert.IsType<SingleAttackIntent>(intent)
                .GetSingleDamage(targets, monster.Creature)),
            intent => Assert.IsType<DebuffIntent>(intent));
        int[] hpBefore = targets.Select(target => target.CurrentHp).ToArray();
        await monster.PerformMove();
        Assert.Equal(hpBefore.Select(hp => hp - expectedSporeDamage), targets.Select(target => target.CurrentHp));
        Assert.All(targets, target => Assert.Equal(2, Assert.IsType<FrailPower>(target.GetPower<FrailPower>()).Amount));

        (_, monster, targets) = Task14MonsterTestFixture.CreateCombat<Flyconid>(
            ascension,
            "flyconid-smash-effect",
            targetCount: 2);
        Task14MonsterTestFixture.ForceMove(monster, "SMASH_MOVE");
        Assert.Equal(expectedSmashDamage, Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove!.Intents))
            .GetSingleDamage(targets, monster.Creature));
        hpBefore = targets.Select(target => target.CurrentHp).ToArray();
        await monster.PerformMove();
        Assert.Equal(hpBefore.Select(hp => hp - expectedSmashDamage), targets.Select(target => target.CurrentHp));
    }

    private static void AssertBranch(RandomBranchState.StateWeight state, string expectedStateId, float expectedWeight)
    {
        Assert.Equal(expectedStateId, state.StateId);
        Assert.Equal(MoveRepeatType.CannotRepeat, state.RepeatType);
        Assert.Equal(expectedStateId == "VULNERABLE_SPORES_MOVE" ? 3 : expectedStateId == "FRAIL_SPORES_MOVE" ? 2 : 0, state.Cooldown);
        Assert.Equal(0, state.MaxTimes);
        Assert.Equal(expectedWeight, state.GetWeight());
    }
}
