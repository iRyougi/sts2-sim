namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class CubexConstructTests : IDisposable
{
    private readonly Task14MonsterTestFixture _fixture = new(typeof(CubexConstruct));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 65)]
    [InlineData((int)AscensionLevel.ToughEnemies, 70)]
    public void Hp_UsesToughEnemiesValue(int ascension, int expectedHp)
    {
        (_, CubexConstruct monster, _) = Task14MonsterTestFixture.CreateCombat<CubexConstruct>(ascension);

        Assert.Equal(expectedHp, monster.MinInitialHp);
        Assert.Equal(expectedHp, monster.MaxInitialHp);
        Assert.Equal(expectedHp, monster.Creature.MaxHp);
    }

    [Fact]
    public async Task StartCombat_SuppressesEntryBlockButPreservesArtifactAndLiveBlockGain()
    {
        (var combatState, CubexConstruct monster, _) =
            Task14MonsterTestFixture.CreateCombat<CubexConstruct>(0);
        var engine = new CombatEngine(combatState);

        await engine.StartCombatAsync();

        Assert.True(engine.IsInProgress);
        Assert.Equal(0, monster.Creature.Block);
        Assert.Equal(1, Assert.IsType<ArtifactPower>(monster.Creature.GetPower<ArtifactPower>()).Amount);

        decimal gained = await CreatureCmd.GainBlock(
            combatState, monster.Creature, 13m, ValueProp.Move, null, null);
        Assert.Equal(13m, gained);
        Assert.Equal(13, monster.Creature.Block);
    }

    [Theory]
    [InlineData(0, 7, 5)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 8, 6)]
    public async Task ChargeTwoIndependentBlastsAndExpel_CompleteCycleWithRealEffects(
        int ascension,
        int expectedBlastBase,
        int expectedExpelBase)
    {
        (_, CubexConstruct monster, IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets) =
            Task14MonsterTestFixture.CreateCombat<CubexConstruct>(ascension);
        var target = Assert.Single(targets);
        MonsterState blastOneState = monster.MoveStateMachine!.States["REPEATER_BLAST_MOVE"];
        MonsterState blastTwoState = monster.MoveStateMachine.States["REPEATER_BLAST_MOVE_2"];
        Assert.IsType<MoveState>(blastOneState);
        Assert.IsType<MoveState>(blastTwoState);
        Assert.NotSame(blastOneState, blastTwoState);

        Assert.Equal("CHARGE_UP_MOVE", monster.NextMove!.StateId);
        Assert.IsType<BuffIntent>(Assert.Single(monster.NextMove.Intents));
        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(2, Assert.IsType<StrengthPower>(monster.Creature.GetPower<StrengthPower>()).Amount);
        AssertBlast(monster, targets, "REPEATER_BLAST_MOVE", expectedBlastBase);

        int hpBefore = target.CurrentHp;
        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(hpBefore - expectedBlastBase - 2, target.CurrentHp);
        Assert.Equal(4, monster.Creature.GetPower<StrengthPower>()!.Amount);
        AssertBlast(monster, targets, "REPEATER_BLAST_MOVE_2", expectedBlastBase);

        hpBefore = target.CurrentHp;
        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(hpBefore - expectedBlastBase - 4, target.CurrentHp);
        Assert.Equal(6, monster.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.Equal("EXPEL_MOVE", monster.NextMove!.StateId);
        MultiAttackIntent expel = Assert.IsType<MultiAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedExpelBase, expel.GetSingleDamage(targets, monster.Creature));
        Assert.Equal(2, expel.Repeats);

        hpBefore = target.CurrentHp;
        await Task14MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.Equal(hpBefore - (2 * (expectedExpelBase + 6)), target.CurrentHp);
        Assert.Equal(6, monster.Creature.GetPower<StrengthPower>()!.Amount);
        AssertBlast(monster, targets, "REPEATER_BLAST_MOVE", expectedBlastBase);
    }

    private static void AssertBlast(
        CubexConstruct monster,
        IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> targets,
        string expectedStateId,
        int expectedDamage)
    {
        Assert.Equal(expectedStateId, monster.NextMove!.StateId);
        Assert.Collection(
            monster.NextMove.Intents,
            intent => Assert.Equal(expectedDamage, Assert.IsType<SingleAttackIntent>(intent)
                .GetSingleDamage(targets, monster.Creature)),
            intent => Assert.IsType<BuffIntent>(intent));
    }
}
