using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Hooks;

[Collection("ModelDb")]
public sealed class CombatCompletionSourceOrderTests : IDisposable
{
    public CombatCompletionSourceOrderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
        ModelDb.Inject(typeof(CompletionBoundaryRelic));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Victory_CheeseChangesHpBeforeBoneChecksThreshold(bool boneFirst)
    {
        var (run, player, room) = CreateCombat();
        if (boneFirst) await RelicCmd.Obtain(ModelDb.Relic<MeatOnTheBone>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<ChosenCheese>(), player);
        if (!boneFirst) await RelicCmd.Obtain(ModelDb.Relic<MeatOnTheBone>(), player);
        await room.Enter(run);
        player.Creature.SetMaxHpInternal(100);
        player.Creature.SetCurrentHpInternal(50);
        await CreatureCmd.Kill(room.Engine.State.Enemies.Single());
        room.Engine.CheckWinCondition();

        await room.ResolveOutcomeAsync(generateRewards: false);

        Assert.Equal(101, player.Creature.MaxHp);
        Assert.Equal(51, player.Creature.CurrentHp);
        await room.Exit(run);
        await room.ResolveOutcomeAsync(generateRewards: false);
        Assert.Equal(101, player.Creature.MaxHp);
        Assert.Equal(51, player.Creature.CurrentHp);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Victory_CompletesEndAndCleanupBeforeVictoryEvenWithoutRewards(bool generateRewards)
    {
        var (run, player, room) = CreateCombat();
        await RelicCmd.Obtain(ModelDb.Relic<CompletionBoundaryRelic>(), player);
        var probe = player.Relics.OfType<CompletionBoundaryRelic>().Single();
        await room.Enter(run);
        await PowerCmd.Apply<StrengthPower>(room.Engine.State, player.Creature, 3, player.Creature, null);
        player.Creature.GainBlockInternal(9);
        await CreatureCmd.Kill(room.Engine.State.Enemies.Single());
        room.Engine.CheckWinCondition();

        probe.Reenter = () => { _ = room.Exit(run); };
        probe.VictoryRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task outcome = room.ResolveOutcomeAsync(generateRewards);
        Task exit = room.Exit(run);
        try
        {
            Assert.True(probe.ReentryRejected);
            Assert.False(exit.IsCompleted);
            Assert.NotNull(player.PlayerCombatState);
            Assert.NotNull(player.Creature.CombatState);
        }
        finally
        {
            probe.VictoryRelease.TrySetResult();
        }
        await Task.WhenAll(outcome, exit);

        Assert.Equal(1, probe.EndCount);
        Assert.True(probe.EndSawCombatPowers);
        Assert.True(probe.VictorySawCleanedContext);
        Assert.Equal(1, probe.VictoryCount);
        Assert.Equal(generateRewards ? 1 : 0, probe.OfferCount);
        await room.Exit(run);
        await room.Exit(run);
        Assert.Equal(1, probe.EndCount);
        Assert.Equal(1, probe.VictoryCount);
        Assert.Null(player.PlayerCombatState);
        Assert.Null(player.Creature.CombatState);
    }

    private static (RunState, Player, CombatRoom) CreateCombat()
    {
        var run = new RunState("completion-source-order", new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        return (run, player, new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            RoomType.Monster, CombatRoom.ForcedEncounterName));
    }
}
