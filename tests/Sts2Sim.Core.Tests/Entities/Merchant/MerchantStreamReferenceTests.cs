using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
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
    public void MerchantGeneration_AndRestock_KeepRarityAndUpgradeRollsOnShops()
    {
        var player = NewPlayer("8F9CPYQ6QYEN");
        int before = player.PlayerRng.Rewards.Counter;
        var nextReward = player.PlayerRng.Rewards.CloneExact();
        float oddsBefore = player.Odds.CardRarity.CurrentValue;
        MerchantInventory inventory = MerchantInventory.Generate(player);
        // v0.113.1: the five rarity, seven mandatory upgrade and two relic rarity
        // rolls now share Shops with selection/prices, including restocks.
        Assert.Equal(before, player.PlayerRng.Rewards.Counter);
        int shopsBefore = player.PlayerRng.Shops.Counter;
        inventory.Restock(inventory.Cards[0]);
        Assert.Equal(shopsBefore + 4, player.PlayerRng.Shops.Counter);
        shopsBefore = player.PlayerRng.Shops.Counter;
        inventory.Restock(inventory.Cards.Single(c => c.Card.IsColorless && c.Card.Rarity == CardRarity.Rare));
        Assert.Equal(shopsBefore + 3, player.PlayerRng.Shops.Counter);
        shopsBefore = player.PlayerRng.Shops.Counter;
        inventory.Restock(inventory.Relics[0]);
        Assert.Equal(shopsBefore + 2, player.PlayerRng.Shops.Counter);
        Assert.Equal(before, player.PlayerRng.Rewards.Counter);
        Assert.Equal(nextReward.NextInt(), player.PlayerRng.Rewards.NextInt());
        Assert.Equal(oddsBefore, player.Odds.CardRarity.CurrentValue);

        // Source order: eager relics, eager potion batch/prices, lazy cards,
        // then Setup's sale index and separate sale reprice. Counters alone
        // cannot distinguish the former v0.111 card-first orchestration.
        Player keyed = NewPlayer("918-merchant-order", keyed: true);
        _ = MerchantInventory.Generate(keyed);
        const string unit = "NextFloat(min=0,max=1)";
        const string relicPrice = "NextFloat(min=0.85,max=1.15)";
        const string cardPrice = "NextFloat(min=0.95,max=1.05)";
        var expected = new List<string> { unit, unit, relicPrice, relicPrice, relicPrice };
        for (int i = 0; i < 3; i++) expected.AddRange([unit, "I"]);
        expected.AddRange([cardPrice, cardPrice, cardPrice]);
        for (int i = 0; i < 5; i++) expected.AddRange([unit, "I", unit, cardPrice]);
        for (int i = 0; i < 2; i++) expected.AddRange(["I", unit, cardPrice]);
        expected.AddRange(["I", cardPrice]);
        Assert.Equal(expected, keyed.PlayerRng.GetKeyedDraws()
            .Where(draw => draw.StreamName == "shops")
            .Select(draw => draw.Operation.StartsWith("NextInt(", StringComparison.Ordinal) ? "I" : draw.Operation));
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

    private static Player NewPlayer(string seed, bool keyed = false)
    {
        var run = keyed ? RunState.CreateKeyedForLabels(seed, [new Overgrowth()], 10)
            : new RunState(seed, ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        return player;
    }

    public void Dispose() => ModelDb.ResetForTests();
}
