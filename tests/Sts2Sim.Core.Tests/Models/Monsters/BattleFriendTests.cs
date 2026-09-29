namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class BattleFriendTests : IDisposable
{
    public BattleFriendTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(typeof(BattleFriendV1), 75)]
    [InlineData(typeof(BattleFriendV2), 150)]
    [InlineData(typeof(BattleFriendV3), 300)]
    public async Task BattleFriend_AfterAddedToRoom_IsEnemyWithExpectedHpAndTimeLimit(
        Type monsterType,
        int expectedHp)
    {
        (CombatState state, MonsterModel monster) = await CreateEnemyAsync(monsterType);

        Creature enemy = Assert.Single(state.Enemies);
        Assert.Same(monster.Creature, enemy);
        Assert.Equal(CombatSide.Enemy, enemy.Side);
        Assert.Equal(expectedHp, enemy.MaxHp);
        Assert.Equal(expectedHp, enemy.CurrentHp);
        BattlewornDummyTimeLimitPower timeLimit = Assert.IsType<BattlewornDummyTimeLimitPower>(
            enemy.GetPower<BattlewornDummyTimeLimitPower>());
        Assert.Equal(PowerType.Buff, timeLimit.Type);
        Assert.Equal(3, timeLimit.Amount);
        Assert.Null(timeLimit.Applier);
    }

    [Fact]
    public async Task BattleFriend_TimeLimit_EnemyTurnEndsCountThreeToTwoToOneThenEscapes()
    {
        (CombatState state, MonsterModel monster) = await CreateEnemyAsync(typeof(BattleFriendV1));
        Creature enemy = monster.Creature;

        await Hook.AfterSideTurnEnd(state, CombatSide.Enemy, state.Enemies);
        Assert.Equal(2, enemy.GetPower<BattlewornDummyTimeLimitPower>()!.Amount);

        await Hook.AfterSideTurnEnd(state, CombatSide.Enemy, state.Enemies);
        Assert.Equal(1, enemy.GetPower<BattlewornDummyTimeLimitPower>()!.Amount);

        await Hook.AfterSideTurnEnd(state, CombatSide.Enemy, state.Enemies);

        Assert.False(enemy.IsDead);
        Assert.DoesNotContain(enemy, state.Enemies);
        Assert.Contains(enemy, state.EscapedCreatures);
    }

    [Fact]
    public async Task BattleFriend_TimeLimit_EscapesAfterOtherDurationPowersTick()
    {
        (CombatState state, MonsterModel monster) = await CreateEnemyAsync(typeof(BattleFriendV1));
        Creature enemy = monster.Creature;

        await Hook.AfterSideTurnEnd(state, CombatSide.Enemy, state.Enemies);
        await Hook.AfterSideTurnEnd(state, CombatSide.Enemy, state.Enemies);
        await Sts2Sim.Core.Commands.PowerCmd.Apply<WeakPower>(state, enemy, 2m, null, null);

        await Hook.AfterSideTurnEnd(state, CombatSide.Enemy, state.Enemies);

        Assert.False(enemy.IsDead);
        Assert.DoesNotContain(enemy, state.Enemies);
        Assert.Contains(enemy, state.EscapedCreatures);
    }
    private static async Task<(CombatState State, MonsterModel Monster)> CreateEnemyAsync(Type monsterType)
    {
        var state = new CombatState(new RunState($"battle-friend-{monsterType.Name}", new Overgrowth()));
        state.AddPlayerCreature(Creature.CreateStandaloneForTests(1_000, 1_000));
        var monster = (MonsterModel)ModelDb.Get(monsterType).MutableClone();
        state.AddMonster(monster, CombatSide.Enemy);
        monster.SetUpForCombat();
        await monster.AfterAddedToRoom();
        monster.RollMove(state.Allies);
        return (state, monster);
    }
}
