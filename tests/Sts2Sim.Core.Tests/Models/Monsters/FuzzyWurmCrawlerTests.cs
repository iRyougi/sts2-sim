namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class FuzzyWurmCrawlerTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(FuzzyWurmCrawler));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 55, 57)]
    [InlineData((int)AscensionLevel.ToughEnemies, 58, 59)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (FuzzyWurmCrawler monster, _) = Task13MonsterTestFixture.CreateCombat<FuzzyWurmCrawler>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 6)]
    public async Task AcidGoopAndInhale_CompleteCycleAndLoopBack(int ascension, int expectedAcidDamage)
    {
        (FuzzyWurmCrawler monster, var targets) =
            Task13MonsterTestFixture.CreateCombat<FuzzyWurmCrawler>(ascension);
        var target = Assert.Single(targets);

        AssertAttack(monster, targets, "FIRST_ACID_GOOP", expectedAcidDamage);
        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedAcidDamage, target.CurrentHp);
        Assert.Equal("INHALE", monster.NextMove!.StateId);
        Assert.IsType<BuffIntent>(Assert.Single(monster.NextMove.Intents));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(7, monster.Creature.GetPower<StrengthPower>()!.Amount);
        AssertAttack(monster, targets, "ACID_GOOP", expectedAcidDamage);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedAcidDamage - (expectedAcidDamage + 7), target.CurrentHp);
        AssertAttack(monster, targets, "FIRST_ACID_GOOP", expectedAcidDamage);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(200 - expectedAcidDamage - (2 * (expectedAcidDamage + 7)), target.CurrentHp);
        Assert.Equal("INHALE", monster.NextMove!.StateId);
    }

    private static void AssertAttack(
        FuzzyWurmCrawler monster,
        IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets,
        string stateId,
        int expectedDamage)
    {
        Assert.Equal(stateId, monster.NextMove!.StateId);
        Assert.Equal(expectedDamage, Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents))
            .GetSingleDamage(targets, monster.Creature));
    }
}
