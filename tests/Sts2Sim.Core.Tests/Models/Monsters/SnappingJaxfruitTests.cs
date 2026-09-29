namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class SnappingJaxfruitTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(SnappingJaxfruit));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 31, 33)]
    [InlineData((int)AscensionLevel.ToughEnemies, 34, 36)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (SnappingJaxfruit monster, _) = Task13MonsterTestFixture.CreateCombat<SnappingJaxfruit>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 4)]
    public async Task EnergyOrb_AttacksBuffsAndSelfLoopsForTwoRounds(int ascension, int expectedDamage)
    {
        (SnappingJaxfruit monster, var targets) =
            Task13MonsterTestFixture.CreateCombat<SnappingJaxfruit>(ascension);
        var target = Assert.Single(targets);

        Assert.Equal("ENERGY_ORB_MOVE", monster.NextMove!.StateId);
        Assert.Collection(
            monster.NextMove.Intents,
            intent => Assert.Equal(expectedDamage, Assert.IsType<SingleAttackIntent>(intent)
                .GetSingleDamage(targets, monster.Creature)),
            intent => Assert.IsType<BuffIntent>(intent));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedDamage, target.CurrentHp);
        Assert.Equal(2, monster.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.Equal("ENERGY_ORB_MOVE", monster.NextMove!.StateId);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedDamage - (expectedDamage + 2), target.CurrentHp);
        Assert.Equal(4, monster.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.Equal("ENERGY_ORB_MOVE", monster.NextMove!.StateId);
    }
}
