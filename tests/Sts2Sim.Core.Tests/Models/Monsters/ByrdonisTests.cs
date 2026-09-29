namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class ByrdonisTests : IDisposable
{
    private readonly Task14MonsterTestFixture _fixture = new(typeof(Byrdonis));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 81, 84)]
    [InlineData((int)AscensionLevel.ToughEnemies, 90, 90)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (_, Byrdonis monster, _) = Task14MonsterTestFixture.CreateCombat<Byrdonis>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 17, 3)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 19, 4)]
    public async Task SwoopAndPeck_AlternateForTwoFullCycles(
        int ascension,
        int expectedSwoopDamage,
        int expectedPeckDamage)
    {
        (_, Byrdonis monster, IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets) =
            Task14MonsterTestFixture.CreateCombat<Byrdonis>(ascension);
        var target = Assert.Single(targets);

        AssertSingleAttack(monster, targets, "SWOOP_MOVE", expectedSwoopDamage);
        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(1_000_000 - expectedSwoopDamage, target.CurrentHp);
        AssertPeck(monster, targets, expectedPeckDamage);

        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(1_000_000 - expectedSwoopDamage - (3 * expectedPeckDamage), target.CurrentHp);
        AssertSingleAttack(monster, targets, "SWOOP_MOVE", expectedSwoopDamage);

        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        AssertPeck(monster, targets, expectedPeckDamage);

        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(1_000_000 - (2 * expectedSwoopDamage) - (6 * expectedPeckDamage), target.CurrentHp);
        AssertSingleAttack(monster, targets, "SWOOP_MOVE", expectedSwoopDamage);
    }

    [Fact]
    public async Task BeforeCombatStart_InstallsTerritorialAndEnemyTurnEndGrantsStrength()
    {
        (CombatState combatState, Byrdonis monster, _) =
            Task14MonsterTestFixture.CreateCombat<Byrdonis>(0);

        await Hook.BeforeCombatStart(combatState);

        TerritorialPower territorial = Assert.IsType<TerritorialPower>(monster.Creature.GetPower<TerritorialPower>());
        Assert.Equal(1, territorial.Amount);
        Assert.Null(monster.Creature.GetPower<StrengthPower>());

        await Hook.AfterSideTurnEnd(combatState, CombatSide.Player, combatState.Allies);
        Assert.Null(monster.Creature.GetPower<StrengthPower>());

        await Hook.AfterSideTurnEnd(combatState, CombatSide.Enemy, new[] { monster.Creature });
        Assert.Equal(1, Assert.IsType<StrengthPower>(monster.Creature.GetPower<StrengthPower>()).Amount);
    }

    private static void AssertSingleAttack(
        Byrdonis monster,
        IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets,
        string expectedStateId,
        int expectedDamage)
    {
        Assert.Equal(expectedStateId, monster.NextMove!.StateId);
        Assert.Equal(expectedDamage, Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents))
            .GetSingleDamage(targets, monster.Creature));
    }

    private static void AssertPeck(
        Byrdonis monster,
        IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets,
        int expectedDamage)
    {
        Assert.Equal("PECK_MOVE", monster.NextMove!.StateId);
        MultiAttackIntent peck = Assert.IsType<MultiAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedDamage, peck.GetSingleDamage(targets, monster.Creature));
        Assert.Equal(3, peck.Repeats);
    }
}
