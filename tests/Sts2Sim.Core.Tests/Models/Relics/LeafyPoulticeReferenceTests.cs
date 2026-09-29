using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class LeafyPoulticeReferenceTests : IDisposable
{
    public LeafyPoulticeReferenceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact]
    public async Task ArchivedDG9FloorOne_TransformsStrikeThenDefendToRecordedCards()
    {
        const string seed = "DG9V3VA4H6KP";
        var run = new RunState(seed, ActDefinition.GetRandomList(seed), ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var originalCards = player.Deck.Cards.ToHashSet();
        int nicheBefore = run.Rng.Niche.Counter;
        int transformationsBefore = player.PlayerRng.Transformations.Counter;

        await RelicCmd.Obtain(ModelDb.Relic<LeafyPoultice>(), player);

        // Direct archive evidence: DG9V3VA4H6KP/run_history.json F1 cards_transformed
        // records STRIKE_SILENT -> FLECHETTES, then DEFEND_SILENT -> INFINITE_BLADES.
        // No reference card factory, RNG reconstruction, or archive result injection.
        Assert.Equal(new[] { "FLECHETTES", "INFINITE_BLADES" },
            player.Deck.Cards.Where(card => !originalCards.Contains(card)).Select(card => card.Id.Entry));
        Assert.Equal(nicheBefore, run.Rng.Niche.Counter);
        Assert.Equal(transformationsBefore + 2, player.PlayerRng.Transformations.Counter);
    }

    public void Dispose() => ModelDb.ResetForTests();
}
