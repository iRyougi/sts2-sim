using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Merchant;

[Collection("ModelDb")]
public sealed class SilentMerchantPoolReferenceTests : IDisposable
{
    public SilentMerchantPoolReferenceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact]
    public void NormalSilentShops_OnlyOfferUnlockedCharacterSharedAndColorlessPools()
    {
        var run = new RunState("8F9CPYQ6QYEN", ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var characterIds = player.Character.CardPool.GetUnlockedCards(player.UnlockState, false)
            .Select(card => card.Id).ToHashSet();
        var colorlessIds = ColorlessCardPool.Instance.GetUnlockedCards(player.UnlockState, false)
            .Select(card => card.Id).ToHashSet();
        var potionIds = PotionFactory.GetOutOfCombatPool(player).Select(potion => potion.Id).ToHashSet();

        // Source pool contract; F22 archive revealed Regent cards sold to Silent.
        // Two real generations exercise successive stock without fitting seed or RNG counters.
        for (int shop = 0; shop < 2; shop++)
        {
            var inventory = MerchantInventory.Generate(player);
            Assert.Equal(7, inventory.Cards.Count);
            Assert.All(inventory.Cards.Take(5), entry => Assert.Contains(entry.Card.Id, characterIds));
            Assert.All(inventory.Cards.Skip(5), entry => Assert.Contains(entry.Card.Id, colorlessIds));
            Assert.All(inventory.Potions, entry => Assert.Contains(entry.Potion.Id, potionIds));
        }
    }

    public void Dispose() => ModelDb.ResetForTests();
}
