using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class CurseCombatGenerationTests : IDisposable
{
    public CurseCombatGenerationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    // Game Greed / PoorSleep / SporeMind override only CanBeGeneratedByModifiers; CardModel.CanBeGeneratedInCombat
    // defaults to true, so CardFactory.GetFilteredTransformationOptions keeps them for an in-combat curse transform
    // (e.g. Entropy). Only AscendersBane overrides CanBeGeneratedInCombat among curses.
    [Fact]
    public void InCombatRandomCurseTransform_PoolIncludesGreedPoorSleepSporeMind()
    {
        var run = new RunState("curse-combat-pool", new Overgrowth(), 0);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var original = (CardModel)ModelDb.Card<Doubt>().MutableClone();
        original.AssignOwner(player);

        Type[] inCombat = CardFactory.GetDefaultTransformationOptions(original, isInCombat: true)
            .Select(card => card.GetType()).ToArray();

        Assert.Contains(typeof(Greed), inCombat);
        Assert.Contains(typeof(PoorSleep), inCombat);
        Assert.Contains(typeof(SporeMind), inCombat);
        Assert.DoesNotContain(typeof(AscendersBane), inCombat);
        Assert.Equal(16, inCombat.Length);
        Assert.False(ModelDb.Card<Greed>().CanBeGeneratedByModifiers);
        Assert.False(ModelDb.Card<PoorSleep>().CanBeGeneratedByModifiers);
        Assert.False(ModelDb.Card<SporeMind>().CanBeGeneratedByModifiers);
    }
}
