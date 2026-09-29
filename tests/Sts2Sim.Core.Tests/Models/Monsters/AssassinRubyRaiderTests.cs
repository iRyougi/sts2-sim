namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class AssassinRubyRaiderTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(AssassinRubyRaider));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 18, 23)]
    [InlineData((int)AscensionLevel.ToughEnemies, 19, 24)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (AssassinRubyRaider monster, _) = Task13MonsterTestFixture.CreateCombat<AssassinRubyRaider>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 11)]
    public async Task Killshot_DealsDamageAndSelfLoopsForTwoRounds(int ascension, int expectedDamage)
    {
        (AssassinRubyRaider monster, var targets) =
            Task13MonsterTestFixture.CreateCombat<AssassinRubyRaider>(ascension);
        var target = Assert.Single(targets);

        Assert.Equal("KILLSHOT_MOVE", monster.NextMove!.StateId);
        var intent = Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedDamage, intent.GetSingleDamage(targets, monster.Creature));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedDamage, target.CurrentHp);
        Assert.Equal("KILLSHOT_MOVE", monster.NextMove!.StateId);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - (2 * expectedDamage), target.CurrentHp);
        Assert.Equal("KILLSHOT_MOVE", monster.NextMove!.StateId);
    }
}
