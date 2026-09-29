using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class ToricToughnessTests : IDisposable
{
    public ToricToughnessTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[] { typeof(Task11NoDamageMonster) }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 7)]
    public async Task Play_StoresActualBlockAndAppliesInstancedPower(bool upgraded, int expectedBlock)
    {
        (var player, _) = await Task11CombatTestSupport.CreateCombatAsync(
            $"task11-toric-{upgraded}");
        ToricToughness card = Task11CombatTestSupport.AddToHand<ToricToughness>(player);
        if (upgraded)
        {
            card.Upgrade();
        }

        await card.PlayAsync(target: null);

        ToricToughnessPower power = Assert.Single(player.Creature.Powers.OfType<ToricToughnessPower>());
        Assert.Equal(expectedBlock, player.Creature.Block);
        Assert.Equal(expectedBlock, power.StoredBlock);
        Assert.Equal(2, power.Amount);
        Assert.Equal(PowerType.Buff, power.Type);
        Assert.Equal(PowerStackType.Counter, power.StackType);
        Assert.Equal(PowerInstanceType.Instanced, power.InstanceType);
    }

    [Fact]
    public async Task BlockClearAttempt_WhenGenuine_RegrantsStoredBlockAndConsumesPowerUntilExpired()
    {
        // 覆盖真正被清空（未被否决）的路径：回合开始先清格挡，然后派发 AfterBlockCleared；
        // ToricToughnessPower 重发存储格挡并消耗一层，两个回合耗尽后移除自身。
        (var player, var room) = await Task11CombatTestSupport.CreateCombatAsync<Task11NoDamageMonster>(
            "task11-toric-genuine-clear");
        ToricToughness card = Task11CombatTestSupport.AddToHand<ToricToughness>(player);
        await card.PlayAsync(target: null);
        Assert.True(Hook.ShouldClearBlock(room.Engine.State, player.Creature));
        Assert.Equal(5, player.Creature.Block);

        await room.Engine.EndPlayerTurnAsync();

        ToricToughnessPower power = Assert.Single(player.Creature.Powers.OfType<ToricToughnessPower>());
        Assert.Equal(5, player.Creature.Block);
        Assert.Equal(1, power.Amount);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(5, player.Creature.Block);
        Assert.Empty(player.Creature.Powers.OfType<ToricToughnessPower>());
    }

    [Fact]
    public async Task BlockClearAttempt_WhenVetoed_StillRegrantsAndConsumesPower()
    {
        // 原生 CombatManager 在 Creature.AfterTurnStart 之后无条件派发 AfterBlockCleared。
        // SturdyClamp 阻止清空并保留至多 10 点，但 ToricToughnessPower 仍重发 5 点并递减。
        (var player, var room) = await Task11CombatTestSupport.CreateCombatAsync<Task11NoDamageMonster>(
            "task11-toric-veto");
        await RelicCmd.Obtain(ModelDb.Relic<SturdyClamp>(), player);
        ToricToughness card = Task11CombatTestSupport.AddToHand<ToricToughness>(player);
        await card.PlayAsync(target: null);
        Assert.False(Hook.ShouldClearBlock(room.Engine.State, player.Creature));
        Assert.Equal(5, player.Creature.Block);

        await room.Engine.EndPlayerTurnAsync();

        ToricToughnessPower power = Assert.Single(player.Creature.Powers.OfType<ToricToughnessPower>());
        Assert.Equal(10, player.Creature.Block);
        Assert.Equal(1, power.Amount);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(15, player.Creature.Block);
        Assert.Empty(player.Creature.Powers.OfType<ToricToughnessPower>());
    }
}
