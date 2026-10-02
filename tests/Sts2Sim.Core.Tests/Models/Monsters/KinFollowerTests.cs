namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class KinFollowerTests : IDisposable
{
    private readonly Task20BossTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 58, 59)]
    [InlineData((int)AscensionLevel.ToughEnemies, 62, 63)]
    public async Task HpOpeningAndMinionLifecycle_UseAuthoritativeValues(
        int ascension,
        int expectedMinHp,
        int expectedMaxHp)
    {
        (KinFollower follower, _, _) =
            await Task20BossTestFixture.CreateSingleCombatAsync<KinFollower>(
                ascension,
                $"kin-follower-lifecycle-{ascension}");

        Assert.Equal(expectedMinHp, follower.MinInitialHp);
        Assert.Equal(expectedMaxHp, follower.MaxInitialHp);
        Assert.InRange(follower.Creature.MaxHp, expectedMinHp, expectedMaxHp);
        Assert.Equal("QUICK_SLASH_MOVE", follower.NextMove!.StateId);
        Assert.Equal(1, Assert.IsType<MinionPower>(
            follower.Creature.GetPower<MinionPower>()).Amount);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 3)]
    public async Task StateGraph_HasExactQuickBoomerangDanceCycleAndIntents(
        int ascension,
        int expectedDanceStrength)
    {
        (KinFollower follower, var room, _) =
            await Task20BossTestFixture.CreateSingleCombatAsync<KinFollower>(
                ascension,
                $"kin-follower-structure-{ascension}");
        MonsterMoveStateMachine machine = follower.MoveStateMachine!;
        MoveState quick = Assert.IsType<MoveState>(machine.States["QUICK_SLASH_MOVE"]);
        MoveState boomerang = Assert.IsType<MoveState>(machine.States["BOOMERANG_MOVE"]);
        MoveState dance = Assert.IsType<MoveState>(machine.States["POWER_DANCE_MOVE"]);

        Assert.Equal(3, machine.States.Count);
        Assert.Equal(5, Assert.IsType<SingleAttackIntent>(Assert.Single(quick.Intents))
            .GetSingleDamage(room.Engine.State.Allies, follower.Creature));
        MultiAttackIntent boomerangIntent = Assert.IsType<MultiAttackIntent>(
            Assert.Single(boomerang.Intents));
        Assert.Equal(2,
            boomerangIntent.GetSingleDamage(room.Engine.State.Allies, follower.Creature));
        Assert.Equal(2, boomerangIntent.Repeats);
        Assert.IsType<BuffIntent>(Assert.Single(dance.Intents));
        Assert.Same(boomerang, quick.FollowUpState);
        Assert.Same(dance, boomerang.FollowUpState);
        Assert.Same(quick, dance.FollowUpState);

        // A minion alone is already in the ending window; retain its primary opponent.
        room.Engine.State.AddMonster(
            (WanderingGrunt)Sts2Sim.Core.Models.ModelDb.Monster<WanderingGrunt>().MutableClone(),
            Sts2Sim.Core.Combat.CombatSide.Enemy);
        Task20BossTestFixture.ForceMove(follower, "POWER_DANCE_MOVE");
        await follower.PerformMove();
        Assert.Equal(expectedDanceStrength, Assert.IsType<StrengthPower>(
            follower.Creature.GetPower<StrengthPower>()).Amount);
    }

    [Fact]
    public async Task QuickAndBoomerang_DealRealFixedDamageToEveryPlayer()
    {
        (KinFollower follower, _, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateSingleCombatAsync<KinFollower>(
                0,
                "kin-follower-multiplayer",
                playerCount: 2);
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();

        await follower.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(follower);
        Assert.Equal(hpBefore.Select(hp => hp - 5),
            players.Select(player => player.Creature.CurrentHp));

        await follower.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(follower);
        Assert.Equal(hpBefore.Select(hp => hp - 9),
            players.Select(player => player.Creature.CurrentHp));
        Assert.Equal("POWER_DANCE_MOVE", follower.NextMove!.StateId);
    }
}
