using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Hooks;

[Collection("ModelDb")]
public sealed class DeathBoundarySourceTests : IDisposable
{
    public DeathBoundarySourceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
        ModelDb.Inject(typeof(DeathBoundaryRelic));
    }
    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LethalLoss_NotifiesDeathBeforePreventionAndRetriesUnhealedVeto(bool heal)
    {
        var (run, player, room) = await CreateCombat();
        await RelicCmd.Obtain(ModelDb.Relic<DeathBoundaryRelic>(), player);
        var probe = player.Relics.OfType<DeathBoundaryRelic>().Single();
        probe.HealOnPrevention = heal;

        await CreatureCmd.Kill(player.Creature);

        Assert.Equal(heal ? new[] { "before", "should", "death:True:0", "prevent" } :
            new[] { "before", "should", "death:True:0", "prevent", "before", "should", "death:False:0" }, probe.Trace);
        Assert.Equal(heal ? 10 : 0, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task OrdinaryEnemyDeath_RemovesCurrentMembershipAfterCallbacksButRetainsHistory()
    {
        var (_, _, room) = await CreateCombat();
        var enemy = room.Engine.State.Enemies.Single();

        await CreatureCmd.Kill(enemy);

        Assert.DoesNotContain(enemy, room.Engine.State.Enemies);
        Assert.Contains(enemy, room.Engine.State.SpawnedEnemies);
        Assert.Null(enemy.CombatState);
        Assert.Null(await PowerCmd.Apply<Sts2Sim.Core.Models.Powers.CrushUnderPower>(
            room.Engine.State, enemy, 1, null, null));
        Assert.Empty(enemy.Powers);

        // A dead target retained in combat remains eligible: source does not reject IsDead alone.
        var (_, _, retainedRoom) = await CreateCombat();
        var retained = retainedRoom.Engine.State.Enemies.Single();
        // Keep a primary enemy alive so this exercises detached/IsDead eligibility,
        // rather than PowerCmd's independent IsEnding guard.
        retainedRoom.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            Sts2Sim.Core.Combat.CombatSide.Enemy);
        retained.SetCurrentHpInternal(0);
        Assert.NotNull(await PowerCmd.Apply<Sts2Sim.Core.Models.Powers.StrengthPower>(
            retainedRoom.Engine.State, retained, 1, null, null));
    }

    [Fact]
    public async Task EndlessPrevention_StopsAfterBoundedRetries()
    {
        var (_, player, _) = await CreateCombat();
        await RelicCmd.Obtain(ModelDb.Relic<DeathBoundaryRelic>(), player);
        var probe = player.Relics.OfType<DeathBoundaryRelic>().Single();
        probe.HealOnPrevention = false;
        probe.AlwaysPrevent = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatureCmd.Kill(player.Creature));

        Assert.Equal(10, probe.Trace.Count(item => item == "prevent"));
        Assert.DoesNotContain("death:False:0", probe.Trace);
    }

    [Fact]
    public async Task ProjectionDeath_DoesNotCompleteTheRealRoomOrChangeOriginalPlayer()
    {
        var (_, player, room) = await CreateCombat();
        int hp = player.Creature.CurrentHp;
        var originalHand = player.PlayerCombatState!.Hand.Cards.ToArray();
        var projection = room.Engine.State.Clone();

        await CreatureCmd.Kill(projection.Enemies.Single());
        projection.Engine!.CheckWinCondition();

        Assert.True(projection.Engine.Won);
        Assert.True(room.Engine.IsInProgress);
        Assert.False(room.Won);
        Assert.Empty(room.GeneratedRewards);
        Assert.Equal(hp, player.Creature.CurrentHp);
        Assert.Equal(originalHand, player.PlayerCombatState.Hand.Cards);
        Assert.Single(room.Engine.State.Enemies);
    }
    private static async Task<(RunState, Player, CombatRoom)> CreateCombat()
    {
        var run = new RunState("death-source-order", new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        return (run, player, room);
    }
}
