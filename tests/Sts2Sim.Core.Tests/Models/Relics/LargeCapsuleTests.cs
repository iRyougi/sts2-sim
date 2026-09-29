using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class LargeCapsuleTests : IDisposable
{
    public LargeCapsuleTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AfterObtained_PullsAndObtainsTwoFrontRelicsThenAddsRegentBasics()
    {
        Player oracle = CreatePlayer("large-capsule");
        RelicModel firstExpected = RelicFactory.PullNextRelicFromFront(oracle);
        await RelicCmd.Obtain(firstExpected, oracle);
        RelicModel secondExpected = RelicFactory.PullNextRelicFromFront(oracle);

        Player player = CreatePlayer("large-capsule");
        int relicCountBefore = player.Relics.Count;

        await RelicCmd.Obtain(ModelDb.Relic<LargeCapsule>(), player);

        RelicModel[] obtained = player.Relics.Skip(relicCountBefore).ToArray();
        Assert.Equal(3, obtained.Length);
        Assert.IsType<LargeCapsule>(obtained[0]);
        Assert.Equal(firstExpected.GetType(), obtained[1].GetType());
        Assert.Equal(secondExpected.GetType(), obtained[2].GetType());
        Assert.All(obtained, relic => Assert.Same(player, relic.Owner));
        Assert.IsType<StrikeRegent>(player.Deck.Cards[^2]);
        Assert.IsType<DefendRegent>(player.Deck.Cards[^1]);
        Assert.All(player.Deck.Cards.Skip(player.Deck.Cards.Count - 2), card =>
        {
            Assert.False(card.IsCanonical);
            Assert.Same(player, card.Owner);
            Assert.Same(player.Deck, card.Pile);
        });
        Assert.Equal(RelicRarity.Ancient, obtained[0].Rarity);
        Assert.False(obtained[0].HasUponPickupEffect);
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
