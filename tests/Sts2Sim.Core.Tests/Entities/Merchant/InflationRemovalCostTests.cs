using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Merchant;

/// <summary>
/// A6 Inflation：移牌费用 A0 为 <c>75 + 25n</c>、A6+ 为 <c>100 + 50n</c>
/// （<c>MerchantCardRemovalEntry.BaseCost/PriceIncrease</c>）。
/// <para>
/// 本仓库此前把 A6+ 的值硬编码成了唯一值，所以 A0 一直是错的。
/// 本文件同时固定"修复后的 A0"和"A10"，防止再次退化。
/// </para>
/// </summary>
[Collection("ModelDb")]
public sealed class InflationRemovalCostTests : IDisposable
{
    public InflationRemovalCostTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(0, 0, 75)]
    [InlineData(0, 1, 100)]
    [InlineData(0, 2, 125)]
    [InlineData(6, 0, 100)]
    [InlineData(6, 1, 150)]
    [InlineData(10, 0, 100)]
    [InlineData(10, 2, 200)]
    public void CardRemovalPrice_FollowsInflation(int ascensionLevel, int removalsUsed, int expected)
    {
        Assert.Equal(
            expected,
            MerchantCardRemovalEntry.PriceFor(new AscensionManager(ascensionLevel), removalsUsed));
    }

    /// <summary>生成整个商店库存时也必须走同一条公式。</summary>
    [Theory]
    [InlineData(0, 75)]
    [InlineData(10, 100)]
    public void GeneratedInventory_UsesTheAscensionAwarePrice(int ascensionLevel, int expected)
    {
        var run = new RunState("inflation-shop", new Overgrowth(), ascensionLevel);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        MerchantInventory inventory = MerchantInventory.Generate(player);

        Assert.Equal(expected, inventory.CardRemoval.Price);
    }

    /// <summary>价格是纯算术，不能消耗商店 RNG。</summary>
    [Fact]
    public void GeneratedInventory_ConsumesTheSameShopRngAtA0AndA10()
    {
        int a0 = ShopRngUsed(0);
        int a10 = ShopRngUsed(10);

        Assert.Equal(a0, a10);
    }

    private static int ShopRngUsed(int ascensionLevel)
    {
        var run = new RunState("inflation-rng", new Overgrowth(), ascensionLevel);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        int before = player.PlayerRng.Shops.Counter;
        MerchantInventory.Generate(player);
        return player.PlayerRng.Shops.Counter - before;
    }
}
