namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class ShrinkerBeetleTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(ShrinkerBeetle));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 38, 40)]
    [InlineData((int)AscensionLevel.ToughEnemies, 40, 42)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (ShrinkerBeetle monster, _) = Task13MonsterTestFixture.CreateCombat<ShrinkerBeetle>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 7, 13)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 8, 14)]
    public async Task ShrinkOnceThenChompAndStompLoop(int ascension, int expectedChomp, int expectedStomp)
    {
        (ShrinkerBeetle monster, var targets) =
            Task13MonsterTestFixture.CreateCombat<ShrinkerBeetle>(ascension, targetCount: 2);

        Assert.Equal("SHRINKER_MOVE", monster.NextMove!.StateId);
        Assert.Equal(IntentType.DebuffStrong, Assert.IsType<DebuffIntent>(Assert.Single(monster.NextMove.Intents)).IntentType);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.All(targets, target =>
        {
            ShrinkPower shrink = Assert.IsType<ShrinkPower>(target.GetPower<ShrinkPower>());
            Assert.Equal(-1, shrink.Amount);
            Assert.Equal(PowerStackType.Single, shrink.StackType);
            Assert.Same(monster.Creature, shrink.Applier);
        });
        AssertAttack(monster, targets, "CHOMP_MOVE", expectedChomp);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.All(targets, target => Assert.Equal(200 - expectedChomp, target.CurrentHp));
        AssertAttack(monster, targets, "STOMP_MOVE", expectedStomp);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.All(targets, target => Assert.Equal(200 - expectedChomp - expectedStomp, target.CurrentHp));
        AssertAttack(monster, targets, "CHOMP_MOVE", expectedChomp);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.All(targets, target => Assert.Equal(200 - (2 * expectedChomp) - expectedStomp, target.CurrentHp));
        Assert.Equal("STOMP_MOVE", monster.NextMove!.StateId);
        Assert.Equal(1, monster.MoveStateMachine!.StateLog.Count(state => state.Id == "SHRINKER_MOVE"));
    }

    private static void AssertAttack(
        ShrinkerBeetle monster,
        IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets,
        string stateId,
        int expectedDamage)
    {
        Assert.Equal(stateId, monster.NextMove!.StateId);
        Assert.Equal(expectedDamage, Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents))
            .GetSingleDamage(targets, monster.Creature));
    }
}
