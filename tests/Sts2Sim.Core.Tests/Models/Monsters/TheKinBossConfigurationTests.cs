namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class TheKinBossConfigurationTests : IDisposable
{
    private readonly Task20BossTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ThreeBodyConfiguration_UsesDistinctMutableFollowersInExactOrderAndSlots()
    {
        (var room, _) = await Task20BossTestFixture.CreateCombatAsync(
            0,
            "the-kin-configuration",
            playerCount: 1,
            CreateTheKin);
        Creature[] enemies = room.Engine.State.Enemies.ToArray();

        Assert.Collection(
            enemies,
            first =>
            {
                KinFollower follower = Assert.IsType<KinFollower>(first.Monster);
                Assert.True(follower.StartsWithDance);
                Assert.Equal("slot1", first.SlotName);
                Assert.Equal("POWER_DANCE_MOVE", follower.NextMove!.StateId);
            },
            second =>
            {
                KinFollower follower = Assert.IsType<KinFollower>(second.Monster);
                Assert.False(follower.StartsWithDance);
                Assert.Equal("slot2", second.SlotName);
                Assert.Equal("QUICK_SLASH_MOVE", follower.NextMove!.StateId);
            },
            leader =>
            {
                Assert.IsType<KinPriest>(leader.Monster);
                Assert.Equal("leaderSlot", leader.SlotName);
            });
        Assert.NotSame(enemies[0].Monster, enemies[1].Monster);
        Assert.False(ModelDb.Monster<KinFollower>().StartsWithDance);
    }

    [Fact]
    public async Task ConfiguredFight_WinsOnlyAfterBothFollowersAndPriestDie()
    {
        (var room, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateCombatAsync(
                0,
                "the-kin-victory",
                playerCount: 1,
                CreateTheKin);
        Player player = Assert.Single(players);
        Creature[] enemies = room.Engine.State.Enemies.ToArray();

        for (int index = 0; index < enemies.Length; index++)
        {
            Creature enemy = enemies[index];
            DamageResult killed = Assert.Single(await CreatureCmd.Damage(
                room.Engine.State,
                new[] { enemy },
                enemy.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: player.Creature,
                cardSource: null,
                cardPlay: null));

            Assert.True(killed.WasTargetKilled);
            Assert.True(enemy.IsDead);
            Assert.Equal(index == enemies.Length - 1, room.Engine.CheckWinCondition());
        }

        Assert.True(room.Engine.Won);
        Assert.False(room.Engine.IsInProgress);
    }

    [Fact]
    public async Task KillingPriestFirstAlsoKillsBothLivingSecondaryFollowersAndEndsCombat()
    {
        (var room, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateCombatAsync(
                0,
                "the-kin-primary-death-cleanup",
                playerCount: 1,
                CreateTheKin);
        Creature priest = room.Engine.State.Enemies.Single(enemy => enemy.Monster is KinPriest);
        Creature[] followers = room.Engine.State.Enemies
            .Where(enemy => enemy.Monster is KinFollower)
            .ToArray();

        DamageResult killed = Assert.Single(await CreatureCmd.Damage(
            room.Engine.State,
            new[] { priest },
            priest.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: players[0].Creature,
            cardSource: null,
            cardPlay: null));

        Assert.True(killed.WasTargetKilled);
        Assert.True(priest.IsDead);
        Assert.All(followers, follower => Assert.True(follower.IsDead));
        Assert.True(room.Engine.CheckWinCondition());
        Assert.True(room.Engine.Won);
    }
    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateTheKin()
    {
        var dancingFollower = (KinFollower)ModelDb.Monster<KinFollower>().MutableClone();
        dancingFollower.StartsWithDance = true;
        return new (MonsterModel Monster, string? SlotName)[]
        {
            (dancingFollower, "slot1"),
            ((KinFollower)ModelDb.Monster<KinFollower>().MutableClone(), "slot2"),
            ((KinPriest)ModelDb.Monster<KinPriest>().MutableClone(), "leaderSlot"),
        };
    }
}
