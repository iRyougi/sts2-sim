namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class AxeRubyRaiderTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(AxeRubyRaider));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 20, 22)]
    [InlineData((int)AscensionLevel.ToughEnemies, 21, 23)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (AxeRubyRaider monster, _) = Task13MonsterTestFixture.CreateCombat<AxeRubyRaider>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 5, 5, 12)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 6, 6, 13)]
    public async Task TwoSwingsAndBigSwing_CompleteCycleAndLoopBack(
        int ascension,
        int expectedSwingDamage,
        int expectedSwingBlock,
        int expectedBigSwingDamage)
    {
        (AxeRubyRaider monster, var targets) = Task13MonsterTestFixture.CreateCombat<AxeRubyRaider>(ascension);
        var target = Assert.Single(targets);

        AssertSwing(monster, targets, "SWING_1", expectedSwingDamage);
        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedSwingDamage, target.CurrentHp);
        Assert.Equal(expectedSwingBlock, monster.Creature.Block);

        AssertSwing(monster, targets, "SWING_2", expectedSwingDamage);
        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - (2 * expectedSwingDamage), target.CurrentHp);
        Assert.Equal(2 * expectedSwingBlock, monster.Creature.Block);

        Assert.Equal("BIG_SWING", monster.NextMove!.StateId);
        Assert.Equal(expectedBigSwingDamage, Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents))
            .GetSingleDamage(targets, monster.Creature));
        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - (2 * expectedSwingDamage) - expectedBigSwingDamage, target.CurrentHp);
        Assert.Equal(2 * expectedSwingBlock, monster.Creature.Block);

        AssertSwing(monster, targets, "SWING_1", expectedSwingDamage);
        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(3 * expectedSwingBlock, monster.Creature.Block);
        Assert.Equal("SWING_2", monster.NextMove!.StateId);
    }

    private static void AssertSwing(
        AxeRubyRaider monster,
        IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets,
        string stateId,
        int expectedDamage)
    {
        Assert.Equal(stateId, monster.NextMove!.StateId);
        Assert.Collection(
            monster.NextMove.Intents,
            intent => Assert.Equal(expectedDamage, Assert.IsType<SingleAttackIntent>(intent)
                .GetSingleDamage(targets, monster.Creature)),
            intent => Assert.IsType<DefendIntent>(intent));
    }
}
