namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class BruteRubyRaiderTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(BruteRubyRaider));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 30, 33)]
    [InlineData((int)AscensionLevel.ToughEnemies, 31, 34)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (BruteRubyRaider monster, _) = Task13MonsterTestFixture.CreateCombat<BruteRubyRaider>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 8)]
    public async Task BeatAndRoar_AlternateForTwoFullCycles(int ascension, int expectedBeatDamage)
    {
        (BruteRubyRaider monster, var targets) =
            Task13MonsterTestFixture.CreateCombat<BruteRubyRaider>(ascension);
        var target = Assert.Single(targets);

        Assert.Equal("BEAT_MOVE", monster.NextMove!.StateId);
        Assert.Equal(expectedBeatDamage, Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents))
            .GetSingleDamage(targets, monster.Creature));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedBeatDamage, target.CurrentHp);
        Assert.Equal("ROAR_MOVE", monster.NextMove!.StateId);
        Assert.IsType<BuffIntent>(Assert.Single(monster.NextMove.Intents));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(3, monster.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.Equal("BEAT_MOVE", monster.NextMove!.StateId);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedBeatDamage - (expectedBeatDamage + 3), target.CurrentHp);
        Assert.Equal("ROAR_MOVE", monster.NextMove!.StateId);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(6, monster.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.Equal("BEAT_MOVE", monster.NextMove!.StateId);
    }
}
