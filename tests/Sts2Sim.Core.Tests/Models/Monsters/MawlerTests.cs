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
    public async Task RandomBranch_AcrossSeedExhaustion_UsesRoarAtMostOnceAndNeverImmediatelyRepeatsAttacks()
    {
        var seen = new HashSet<string>();

        for (int seed = 0; seed < 64; seed++)
        {
            (_, Mawler monster, IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets) =
                Task14MonsterTestFixture.CreateCombat<Mawler>(0, $"mawler-{seed}");
            var sequence = new List<string> { monster.NextMove!.StateId };

            for (int turn = 0; turn < 18; turn++)
            {
                await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
                sequence.Add(monster.NextMove!.StateId);
            }

            Assert.Equal("CLAW_MOVE", sequence[0]);
            Assert.True(sequence.Count(id => id == "ROAR_MOVE") <= 1, string.Join(",", sequence));
            for (int index = 1; index < sequence.Count; index++)
            {
                if (sequence[index] != "ROAR_MOVE")
                {
                    Assert.NotEqual(sequence[index - 1], sequence[index]);
                }
            }

            seen.UnionWith(sequence);
        }

        Assert.Equal(
            new[] { "CLAW_MOVE", "RIP_AND_TEAR_MOVE", "ROAR_MOVE" }.OrderBy(id => id),
            seen.OrderBy(id => id));

        (_, Mawler inspected, _) = Task14MonsterTestFixture.CreateCombat<Mawler>(0, "mawler-weights");
        RandomBranchState branch = Assert.IsType<RandomBranchState>(inspected.MoveStateMachine!.States["RAND"]);
        Assert.Collection(
            branch.States,
            state => AssertBranch(state, "RIP_AND_TEAR_MOVE", MoveRepeatType.CannotRepeat),
            state => AssertBranch(state, "ROAR_MOVE", MoveRepeatType.UseOnlyOnce),
            state => AssertBranch(state, "CLAW_MOVE", MoveRepeatType.CannotRepeat));
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
