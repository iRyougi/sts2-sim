using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class LavaRockTests : IDisposable
{
    public LavaRockTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task BossActOneOwnerReward_AddsTwoPopulatedRelicRewardsOnce()
    {
        RunState runState = CreateRun("lava-rock-boss", out Player owner, out Player foreign);
        await RelicCmd.Obtain(ModelDb.Relic<LavaRock>(), owner);
        LavaRock relic = Assert.Single(owner.Relics.OfType<LavaRock>());
        runState.PushRoom(CreateCombatRoom(RoomType.Boss));
        int rewardsRngBefore = owner.PlayerRng.Rewards.Counter;
        var foreignRewards = new List<Reward>();

        Hook.ModifyRewards(runState, foreign, foreignRewards, RoomType.Boss);
        Assert.Empty(foreignRewards);
        Assert.False(relic.HasTriggered);

        var ownerRewards = new List<Reward>();
        Hook.ModifyRewards(runState, owner, ownerRewards, RoomType.Boss);

        RelicReward[] extras = ownerRewards.Cast<RelicReward>().ToArray();
        Assert.Equal(2, extras.Length);
        Assert.All(extras, reward => Assert.NotNull(reward.Relic));
        Assert.NotSame(extras[0], extras[1]);
        Assert.Equal(rewardsRngBefore + 2, owner.PlayerRng.Rewards.Counter);
        Assert.True(relic.HasTriggered);
        Assert.True(relic.IsUsedUp);

        var repeated = new List<Reward>();
        Hook.ModifyRewards(runState, owner, repeated, RoomType.Boss);
        Assert.Empty(repeated);
        Assert.Equal(rewardsRngBefore + 2, owner.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public async Task CombatRoomOutcome_GeneratesLavaRewardsForOwnerAtPlayerIndexOne()
    {
        // 偏离 #311：最终幕 Boss 不发奖励，故本例须为多幕，让这场 Boss 不是最终幕。
        var runState = new RunState("lava-rock-index-one", [new Overgrowth(), new Hive()]);
        Player first = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(first);
        runState.AddPlayer(owner);
        await RelicCmd.Obtain(ModelDb.Relic<LavaRock>(), owner);
        LavaRock relic = Assert.Single(owner.Relics.OfType<LavaRock>());
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(),
            RoomType.Boss);
        runState.PushRoom(room);
        await room.Enter(runState);
        room.Engine.State.Enemies[0].LoseHpInternal(decimal.MaxValue, default);
        room.Engine.CheckWinCondition();

        await room.ResolveOutcomeAsync();

        Assert.True(relic.HasTriggered);
        Assert.Equal(2, room.GeneratedRewards.Count);
        Assert.Same(room.GeneratedRewards[0], room.Rewards);
        Assert.Empty(room.GeneratedRewards[0].ExtraRewards.OfType<RelicReward>());
        RelicReward[] ownerExtras = room.GeneratedRewards[1].ExtraRewards.OfType<RelicReward>().ToArray();
        Assert.Equal(2, ownerExtras.Length);
        Assert.All(ownerExtras, reward =>
        {
            Assert.NotNull(reward.Relic);
            Assert.False(reward.IsResolved);
        });
    }
    [Fact]
    public async Task NonBossRewardOrNonBossCurrentRoom_DoesNotTrigger()
    {
        RunState runState = CreateRun("lava-rock-room", out Player owner, out _);
        await RelicCmd.Obtain(ModelDb.Relic<LavaRock>(), owner);
        LavaRock relic = Assert.Single(owner.Relics.OfType<LavaRock>());
        runState.PushRoom(CreateCombatRoom(RoomType.Boss));
        var rewards = new List<Reward>();

        Hook.ModifyRewards(runState, owner, rewards, RoomType.Monster);
        Assert.Empty(rewards);
        Assert.False(relic.HasTriggered);

        runState.PopCurrentRoom();
        runState.PushRoom(CreateCombatRoom(RoomType.Monster));
        Hook.ModifyRewards(runState, owner, rewards, RoomType.Boss);
        Assert.Empty(rewards);
        Assert.False(relic.HasTriggered);
    }

    [Fact]
    public void Metadata_MatchesAncientOneShotRelic()
    {
        LavaRock relic = ModelDb.Relic<LavaRock>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasTriggered);
        Assert.False(relic.IsUsedUp);
    }

    private static RunState CreateRun(string seed, out Player owner, out Player foreign)
    {
        var runState = new RunState(seed, new Overgrowth());
        owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(foreign);
        return runState;
    }

    private static CombatRoom CreateCombatRoom(RoomType roomType) =>
        new((Func<MonsterModel>)(() => throw new InvalidOperationException()), roomType);
}