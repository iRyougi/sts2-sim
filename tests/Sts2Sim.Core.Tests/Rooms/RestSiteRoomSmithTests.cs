using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public sealed class RestSiteRoomSmithTests : IDisposable
{
    public RestSiteRoomSmithTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task ResolveAsync_Smith_UpgradesExactlyTheSelectedDeckCard()
    {
        var runState = new RunState("rest-smith", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new RestSiteRoom();
        CardModel selected = player.Deck.Cards.First(card => card.IsUpgradable);
        int upgradableBefore = player.Deck.Cards.Count(card => card.IsUpgradable);

        await room.Enter(runState);
        await room.ResolveAsync(player, new RestSiteDecision.Smith(selected));

        Assert.True(selected.IsUpgraded);
        Assert.Equal(1, player.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.Equal(upgradableBefore - 1, player.Deck.Cards.Count(card => card.IsUpgradable));
    }

    [Fact]
    public async Task ResolveAsync_Smith_RejectsCardOutsidePlayerDeck()
    {
        var runState = new RunState("rest-smith-foreign", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new RestSiteRoom();
        var foreignCard = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();

        await room.Enter(runState);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => room.ResolveAsync(player, new RestSiteDecision.Smith(foreignCard)));

        Assert.Contains("player's deck", exception.Message);
    }
}
