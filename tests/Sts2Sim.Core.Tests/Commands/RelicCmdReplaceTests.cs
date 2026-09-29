using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Commands;

[Collection("ModelDb")]
public sealed class RelicCmdReplaceTests : IDisposable
{
    public RelicCmdReplaceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(ReplaceOldRelic), typeof(ReplaceNewRelic),
            typeof(ReplacingVictoryRelic), typeof(VictoryCountingRelic),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Replace_PreservesIndexOwnerAndLifecycle()
    {
        Player player = CreatePlayer("task11-replace");
        await RelicCmd.Obtain(ModelDb.Relic<ReplaceOldRelic>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<Anchor>(), player);
        ReplaceOldRelic oldRelic = Assert.Single(player.Relics.OfType<ReplaceOldRelic>());
        int index = player.Relics.ToList().IndexOf(oldRelic);
        var newRelic = (ReplaceNewRelic)ModelDb.Relic<ReplaceNewRelic>().MutableClone();

        await RelicCmd.Replace(oldRelic, newRelic);

        Assert.Same(newRelic, player.Relics[index]);
        Assert.Same(player, newRelic.Owner);
        Assert.Equal(1, oldRelic.RemovedCount);
        Assert.Equal(1, newRelic.ObtainedCount);
        Assert.DoesNotContain(oldRelic, player.Relics);
    }

    [Fact]
    public async Task Replace_RejectsForeignOwnedReplacementWithoutMutation()
    {
        Player owner = CreatePlayer("task11-replace-owner");
        Player foreign = CreatePlayer("task11-replace-foreign");
        await RelicCmd.Obtain(ModelDb.Relic<ReplaceOldRelic>(), owner);
        ReplaceOldRelic oldRelic = Assert.Single(owner.Relics.OfType<ReplaceOldRelic>());
        var replacement = (ReplaceNewRelic)ModelDb.Relic<ReplaceNewRelic>().MutableClone();
        replacement.AssignOwner(foreign);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RelicCmd.Replace(oldRelic, replacement));

        Assert.Contains(oldRelic, owner.Relics);
        Assert.DoesNotContain(replacement, owner.Relics);
        Assert.Equal(0, oldRelic.RemovedCount);
    }

    [Fact]
    public async Task CombatVictory_SnapshotsListenersBeforeSelfReplacement()
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            "task11-replace-snapshot");
        await RelicCmd.Obtain(ModelDb.Relic<ReplacingVictoryRelic>(), player);

        await Hook.AfterCombatVictory(room.Engine.State);

        VictoryCountingRelic replacement = Assert.Single(
            player.Relics.OfType<VictoryCountingRelic>());
        Assert.Equal(0, replacement.VictoryCount);
        Assert.Empty(player.Relics.OfType<ReplacingVictoryRelic>());
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
