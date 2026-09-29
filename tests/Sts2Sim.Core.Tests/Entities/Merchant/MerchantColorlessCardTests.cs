using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Merchant;

[Collection("ModelDb")]
public sealed class MerchantColorlessCardTests : IDisposable
{
    public MerchantColorlessCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Generate_HasFiveCharacterCardsAndTwoColorlessCards()
    {
        var runState = new RunState("merchant-colorless", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        MerchantInventory inventory = MerchantInventory.Generate(player);

        Assert.Equal(7, inventory.Cards.Count);
        Assert.All(inventory.Cards.Take(5), entry => Assert.False(entry.Card.IsColorless));
        Assert.All(inventory.Cards.Skip(5), entry => Assert.True(entry.Card.IsColorless));
    }
}
