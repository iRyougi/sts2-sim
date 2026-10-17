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
    public void FixedWitnesses_PreserveInitialBranchesCooldownExpiryFallbackAndRng()
    {
        const string vulnerable = "VULNERABLE_SPORES_MOVE", frail = "FRAIL_SPORES_MOVE", smash = "SMASH_MOVE";
        var witnesses = new[]
        {
            (Seed: 0, Moves: new[] { smash, vulnerable, smash, frail, smash, vulnerable, frail, smash, vulnerable, smash, frail, smash, vulnerable, smash, frail, smash, vulnerable, frail, smash },
                InitialRng: (17041663368176751194UL, 12557946027415917052UL, 3004765070209312485UL, 3047991625224520881UL)),
            (Seed: 2, Moves: new[] { frail, smash, vulnerable, smash, frail, smash, vulnerable, smash, frail, smash, vulnerable, frail, smash, vulnerable, frail, smash, vulnerable, smash, frail },
                InitialRng: (4887817895513884327UL, 4354087154534142974UL, 9946874644757525327UL, 16165317146417626750UL)),
        };
        bool sawFallback = false, sawVulnerableExpiry = false, sawFrailExpiry = false;
        var vulnerableBlockedDistances = new HashSet<int>();
        var frailBlockedDistances = new HashSet<int>();
        foreach (var witness in witnesses)
        {
            (var combat, Flyconid monster, var targets) =
                Task14MonsterTestFixture.CreateCombat<Flyconid>(0, $"flyconid-{witness.Seed}");
            MonsterMoveStateMachine machine = monster.MoveStateMachine!;
            RandomBranchState initial = Assert.IsType<RandomBranchState>(machine.States["INITIAL"]);
            Assert.Collection(initial.States,
                state => AssertBranch(state, frail, 1f),
                state => AssertBranch(state, smash, 1f));
            RandomBranchState random = Assert.IsType<RandomBranchState>(machine.States["RAND"]);
            Assert.Collection(random.States,
                state => AssertBranch(state, vulnerable, 1f),
                state => AssertBranch(state, frail, 1f),
                state => AssertBranch(state, smash, 1f));
            var rng = combat.RunState.Rng.MonsterAi;
            var initialRng = rng.ToSerializable();
            Assert.Equal(1, initialRng.counter);
            Assert.Equal(witness.InitialRng, (initialRng.state0, initialRng.state1, initialRng.state2, initialRng.state3));
            Assert.Equal(witness.Moves[0], monster.NextMove!.StateId);
            Assert.Equal(witness.Moves.Take(1), machine.StateLog.Select(state => state.Id));
            var expectedRng = rng.CloneExact();
            for (int turn = 0; turn < witness.Moves.Length - 1; turn++)
            {
                string[] history = machine.StateLog.Select(state => state.Id).ToArray();
                int lastVulnerable = Array.LastIndexOf(history, vulnerable);
                int lastFrail = Array.LastIndexOf(history, frail);
                int vulnerableDistance = lastVulnerable < 0 ? int.MaxValue : history.Length - lastVulnerable;
                int frailDistance = lastFrail < 0 ? int.MaxValue : history.Length - lastFrail;
                bool zeroEligible = vulnerableDistance <= 3 && frailDistance <= 2 && history[^1] == smash;
                if (vulnerableDistance <= 3) vulnerableBlockedDistances.Add(vulnerableDistance);
                if (frailDistance <= 2) frailBlockedDistances.Add(frailDistance);
                // The all-excluded edge still draws once before returning the first branch.
                expectedRng.NextFloat();
                machine.OnMovePerformed(monster.NextMove!);
                monster.RollMove(targets);
                string current = monster.NextMove!.StateId;
                Assert.True(current == witness.Moves[turn + 1], $"seed={witness.Seed}, turn={turn}");
                Assert.Equal(witness.Moves.Take(turn + 2), machine.StateLog.Select(state => state.Id));
                Assert.Equal(expectedRng.ToSerializable(), rng.ToSerializable());
                if (zeroEligible)
                {
                    Assert.Equal(vulnerable, current);
                    sawFallback = true;
                }
                else
                {
                    if (current == vulnerable) Assert.True(vulnerableDistance > 3);
                    if (current == frail) Assert.True(frailDistance > 2);
                }
                sawVulnerableExpiry |= current == vulnerable && vulnerableDistance == 4;
                sawFrailExpiry |= current == frail && frailDistance == 3;
            }
        }
        Assert.True(sawFallback);
        Assert.True(sawVulnerableExpiry);
        Assert.True(sawFrailExpiry);
        Assert.Equal(new[] { 1, 2, 3 }, vulnerableBlockedDistances.Order());
        Assert.Equal(new[] { 1, 2 }, frailBlockedDistances.Order());
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
