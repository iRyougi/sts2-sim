namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class TwigSlimeSTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(TwigSlimeS));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 7, 11)]
    [InlineData((int)AscensionLevel.ToughEnemies, 8, 12)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (TwigSlimeS monster, _) = Task13MonsterTestFixture.CreateCombat<TwigSlimeS>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 5)]
    public async Task Tackle_DealsDamageAndSelfLoopsForTwoRounds(int ascension, int expectedDamage)
    {
        (TwigSlimeS monster, var targets) = Task13MonsterTestFixture.CreateCombat<TwigSlimeS>(ascension);
        var target = Assert.Single(targets);

        Assert.Equal("TACKLE_MOVE", monster.NextMove!.StateId);
        var intent = Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedDamage, intent.GetSingleDamage(targets, monster.Creature));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedDamage, target.CurrentHp);
        Assert.Equal("TACKLE_MOVE", monster.NextMove!.StateId);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - (2 * expectedDamage), target.CurrentHp);
        Assert.Equal("TACKLE_MOVE", monster.NextMove!.StateId);
    }
}
