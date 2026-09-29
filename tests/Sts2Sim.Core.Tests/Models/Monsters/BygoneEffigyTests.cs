namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class BygoneEffigyTests : IDisposable
{
    private readonly Task14MonsterTestFixture _fixture = new(typeof(BygoneEffigy));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 127)]
    [InlineData((int)AscensionLevel.ToughEnemies, 132)]
    public void Hp_UsesToughEnemiesValue(int ascension, int expectedHp)
    {
        (_, BygoneEffigy monster, _) = Task14MonsterTestFixture.CreateCombat<BygoneEffigy>(ascension);

        Assert.Equal(expectedHp, monster.MinInitialHp);
        Assert.Equal(expectedHp, monster.MaxInitialHp);
        Assert.Equal(expectedHp, monster.Creature.MaxHp);
    }

    [Fact]
    public async Task BeforeCombatStart_InstallsOneSlow()
    {
        (var combatState, BygoneEffigy monster, _) =
            Task14MonsterTestFixture.CreateCombat<BygoneEffigy>(0);

        await Hook.BeforeCombatStart(combatState);

        Assert.Equal(1, Assert.IsType<SlowPower>(monster.Creature.GetPower<SlowPower>()).Amount);
    }

    [Theory]
    [InlineData(0, 13)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 15)]
    public async Task SleepWakeAndSlashes_ReachInfiniteAttackWithoutUnreachableSleepTwo(
        int ascension,
        int expectedSlashBase)
    {
        (_, BygoneEffigy monster, IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets) =
            Task14MonsterTestFixture.CreateCombat<BygoneEffigy>(ascension);
        var target = Assert.Single(targets);

        Assert.Equal(
            new[] { "SLASHES_MOVE", "SLEEP_MOVE", "WAKE_MOVE" },
            monster.MoveStateMachine!.States.Keys.OrderBy(id => id));
        Assert.Equal("SLEEP_MOVE", monster.NextMove!.StateId);
        Assert.IsType<SleepIntent>(Assert.Single(monster.NextMove.Intents));
        int hpBefore = target.CurrentHp;

        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(hpBefore, target.CurrentHp);
        Assert.Equal("WAKE_MOVE", monster.NextMove!.StateId);
        Assert.IsType<BuffIntent>(Assert.Single(monster.NextMove.Intents));

        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(10, Assert.IsType<StrengthPower>(monster.Creature.GetPower<StrengthPower>()).Amount);
        AssertSlash(monster, targets, expectedSlashBase);

        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(hpBefore - expectedSlashBase - 10, target.CurrentHp);
        AssertSlash(monster, targets, expectedSlashBase);

        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(hpBefore - (2 * (expectedSlashBase + 10)), target.CurrentHp);
        AssertSlash(monster, targets, expectedSlashBase);
    }

    private static void AssertSlash(
        BygoneEffigy monster,
        IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets,
        int expectedBaseDamage)
    {
        Assert.Equal("SLASHES_MOVE", monster.NextMove!.StateId);
        Assert.Equal(expectedBaseDamage, Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents))
            .GetSingleDamage(targets, monster.Creature));
    }
}
