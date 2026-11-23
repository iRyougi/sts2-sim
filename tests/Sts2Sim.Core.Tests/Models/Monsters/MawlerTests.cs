namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class MawlerTests : IDisposable
{
    private readonly Task14MonsterTestFixture _fixture = new(typeof(Mawler));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 72)]
    [InlineData((int)AscensionLevel.ToughEnemies, 76)]
    public void Hp_UsesToughEnemiesValue(int ascension, int expectedHp)
    {
        (_, Mawler monster, _) = Task14MonsterTestFixture.CreateCombat<Mawler>(ascension);

        Assert.Equal(expectedHp, monster.MinInitialHp);
        Assert.Equal(expectedHp, monster.MaxInitialHp);
        Assert.Equal(expectedHp, monster.Creature.MaxHp);
    }

    [Theory]
    [InlineData(0, 14, 4)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 16, 5)]
    public async Task Moves_ExposeAuthoritativeIntentsAndEffects(
        int ascension,
        int expectedRipDamage,
        int expectedClawDamage)
    {
        (_, Mawler monster, IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets) =
            Task14MonsterTestFixture.CreateCombat<Mawler>(ascension, targetCount: 2);

        Assert.Equal("CLAW_MOVE", monster.NextMove!.StateId);
        MultiAttackIntent clawIntent = Assert.IsType<MultiAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedClawDamage, clawIntent.GetSingleDamage(targets, monster.Creature));
        Assert.Equal(2, clawIntent.Repeats);
        int[] hpBeforeClaw = targets.Select(target => target.CurrentHp).ToArray();
        await monster.PerformMove();
        Assert.Equal(hpBeforeClaw.Select(hp => hp - (2 * expectedClawDamage)), targets.Select(target => target.CurrentHp));

        Task14MonsterTestFixture.ForceMove(monster, "RIP_AND_TEAR_MOVE");
        SingleAttackIntent ripIntent = Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove!.Intents));
        Assert.Equal(expectedRipDamage, ripIntent.GetSingleDamage(targets, monster.Creature));
        int[] hpBeforeRip = targets.Select(target => target.CurrentHp).ToArray();
        await monster.PerformMove();
        Assert.Equal(hpBeforeRip.Select(hp => hp - expectedRipDamage), targets.Select(target => target.CurrentHp));

        Task14MonsterTestFixture.ForceMove(monster, "ROAR_MOVE");
        Assert.IsType<DebuffIntent>(Assert.Single(monster.NextMove!.Intents));
        await monster.PerformMove();
        Assert.All(targets, target => Assert.Equal(3, Assert.IsType<VulnerablePower>(target.GetPower<VulnerablePower>()).Amount));
    }

    [Fact]
    public void RandomBranch_FixedWitnessesPreserveOneTimeRoarAttackExclusionAndRng()
    {
        const string claw = "CLAW_MOVE", rip = "RIP_AND_TEAR_MOVE", roar = "ROAR_MOVE";
        var witnesses = new[]
        {
            (Seed: 0, Moves: new[] { claw, rip, claw, rip, claw, rip, claw, roar, claw, rip, claw, rip, claw, rip, claw, rip, claw, rip, claw },
                InitialRng: (11856283367011444143UL, 7891399967802905058UL, 3327063325768060044UL, 14531752814712566229UL)),
            (Seed: 1, Moves: new[] { claw, roar, rip, claw, rip, claw, rip, claw, rip, claw, rip, claw, rip, claw, rip, claw, rip, claw, rip },
                InitialRng: (7815036943064807240UL, 13472159203972151296UL, 8917842485399916633UL, 4939493687961313665UL)),
        };
        foreach (var witness in witnesses)
        {
            (var combat, Mawler monster, var targets) =
                Task14MonsterTestFixture.CreateCombat<Mawler>(0, $"mawler-{witness.Seed}");
            MonsterMoveStateMachine machine = monster.MoveStateMachine!;
            RandomBranchState branch = Assert.IsType<RandomBranchState>(machine.States["RAND"]);
            Assert.Collection(branch.States,
                state => AssertBranch(state, rip, MoveRepeatType.CannotRepeat),
                state => AssertBranch(state, roar, MoveRepeatType.UseOnlyOnce),
                state => AssertBranch(state, claw, MoveRepeatType.CannotRepeat));
            var rng = combat.RunState.Rng.MonsterAi;
            var initialRng = rng.ToSerializable();
            Assert.Equal(0, initialRng.counter);
            Assert.Equal(witness.InitialRng, (initialRng.state0, initialRng.state1, initialRng.state2, initialRng.state3));
            Assert.Equal(claw, monster.NextMove!.StateId);
            Assert.Equal(witness.Moves.Take(1), machine.StateLog.Select(state => state.Id));
            var expectedRng = rng.CloneExact();
            int roarCount = 0;
            for (int turn = 0; turn < witness.Moves.Length - 1; turn++)
            {
                string previous = monster.NextMove!.StateId;
                expectedRng.NextFloat();
                machine.OnMovePerformed(monster.NextMove);
                monster.RollMove(targets);
                string current = monster.NextMove!.StateId;
                Assert.True(current == witness.Moves[turn + 1], $"seed={witness.Seed}, turn={turn}");
                Assert.Equal(witness.Moves.Take(turn + 2), machine.StateLog.Select(state => state.Id));
                Assert.Equal(expectedRng.ToSerializable(), rng.ToSerializable());
                if (current == roar) roarCount++;
                else Assert.NotEqual(previous, current);
                Assert.InRange(roarCount, 0, 1);
            }
            Assert.Equal(1, roarCount);
        }
    }

    private static void AssertBranch(
        RandomBranchState.StateWeight state,
        string expectedStateId,
        MoveRepeatType expectedRepeatType)
    {
        Assert.Equal(expectedStateId, state.StateId);
        Assert.Equal(expectedRepeatType, state.RepeatType);
        Assert.Equal(0, state.Cooldown);
        Assert.Equal(1f, state.GetWeight());
    }
}
