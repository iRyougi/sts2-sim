using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Merchant;

[Collection("ModelDb")]
public sealed class MerchantStreamReferenceTests : IDisposable
{
    public MerchantStreamReferenceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact]
    public void MerchantGeneration_ConsumesRarityAndMandatoryUpgradeRewardRolls()
    {
        var player = NewPlayer("8F9CPYQ6QYEN");
        int before = player.PlayerRng.Rewards.Counter;
        _ = MerchantInventory.Generate(player);
        // Upstream CardFactory.CreateForMerchant rolls upgrades even with negative odds:
        // five rarity rolls + seven upgrade rolls + two relic rarity rolls; prices use Shops.
        Assert.Equal(before + 14, player.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public void MerchantPotionShelf_ContainsThreeDistinctPotions()
    {
        // Upstream generates one batch without replacement before assigning prices.
        for (int i = 0; i < 32; i++)
        {
            var inventory = MerchantInventory.Generate(NewPlayer("merchant-potion-batch-" + i));
            Assert.Equal(3, inventory.Potions.Count);
            Assert.Equal(3, inventory.Potions.Select(p => p.Potion.Id).Distinct().Count());
        }
    }

    [Fact]
    public void ColorlessMerchantCards_UseTheirAuthoritativePremiumPriceRange()
    {
        var inventory = MerchantInventory.Generate(NewPlayer("8F9CPYQ6QYEN"));
        var colorless = inventory.Cards.Where(c => c.Card.IsColorless).ToArray();
        Assert.Equal(2, colorless.Length);
        Assert.InRange(colorless.Single(c => c.Card.Rarity == CardRarity.Uncommon).Price, 82, 90);
        Assert.InRange(colorless.Single(c => c.Card.Rarity == CardRarity.Rare).Price, 163, 182);
    }

    private static Player NewPlayer(string seed)
    {
        var run = new RunState(seed, ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        return player;
    }

    public void Dispose() => ModelDb.ResetForTests();
}
