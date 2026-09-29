namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class CrossbowRubyRaiderTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(CrossbowRubyRaider));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 18, 21)]
    [InlineData((int)AscensionLevel.ToughEnemies, 19, 22)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (CrossbowRubyRaider monster, _) = Task13MonsterTestFixture.CreateCombat<CrossbowRubyRaider>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 14)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 16)]
    public async Task ReloadAndFire_AlternateForTwoFullCycles(int ascension, int expectedFireDamage)
    {
        (CrossbowRubyRaider monster, var targets) =
            Task13MonsterTestFixture.CreateCombat<CrossbowRubyRaider>(ascension);
        var target = Assert.Single(targets);

        Assert.Equal("RELOAD_MOVE", monster.NextMove!.StateId);
        Assert.IsType<DefendIntent>(Assert.Single(monster.NextMove.Intents));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(3, monster.Creature.Block);
        Assert.Equal("FIRE_MOVE", monster.NextMove!.StateId);
        Assert.Equal(expectedFireDamage, Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents))
            .GetSingleDamage(targets, monster.Creature));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedFireDamage, target.CurrentHp);
        Assert.Equal("RELOAD_MOVE", monster.NextMove!.StateId);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(6, monster.Creature.Block);
        Assert.Equal("FIRE_MOVE", monster.NextMove!.StateId);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - (2 * expectedFireDamage), target.CurrentHp);
        Assert.Equal("RELOAD_MOVE", monster.NextMove!.StateId);
    }
}
