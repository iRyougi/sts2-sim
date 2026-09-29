using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Commands;

[Collection("ModelDb")]
public sealed class ArchivedReshuffleTests : IDisposable
{
    public ArchivedReshuffleTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task XWDT4877APQF_FirstDiscardReshuffle_DrawsArchivedInstances()
    {
        var run = new RunState("XWDT4877APQF", ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var legSweep = (LegSweep)ModelDb.Card<LegSweep>().MutableClone();
        legSweep.AssignOwner(player);
        await CardPileCmd.AddToDeck(legSweep);
        player.ResetCombatState();
        player.PopulateCombatState(run.Rng.Shuffle);
        var state = new CombatState(run);
        state.AddPlayerCreature(player.Creature);
        var piles = player.PlayerCombatState!;
        var initial = piles.DrawPile.Cards.ToArray();
        // Archive F2 initial ordered cards were independently verified by UID replay.
        Assert.Equal(14, initial.Length);
        Assert.Equal("DEFEND_SILENT", initial[0].Id.Entry);
        Assert.Equal("LEG_SWEEP", initial[13].Id.Entry);
        // F2 player turn 3 players_pre, followed by its two remaining draw-pile cards.
        // This is the recorded precondition, not a recomputation of the shuffle algorithm.
        foreach (int uid in new[] { 2, 5, 4, 6, 1, 3, 7, 10, 12, 8, 9 })
            piles.DiscardPile.AddInternal(initial[uid - 1]);
        piles.ExhaustPile.AddInternal(initial[10]);
        piles.Hand.AddInternal(initial[12]);
        piles.Hand.AddInternal(initial[13]);
        await CardPileCmd.ShuffleIfNecessary(state, player);
        Assert.Equal(new[] { initial[0], initial[9], initial[6] }, piles.DrawPile.Cards.Take(3));
    }
}
