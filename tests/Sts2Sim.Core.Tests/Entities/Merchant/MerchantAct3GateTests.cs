using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Merchant;

[Collection("ModelDb")]
public sealed class MerchantAct3GateTests : IDisposable
{
    private const int RestockSeedCount = 32;
    private const int RestockPurchasesPerRun = 12;

    private static readonly Type[] SellableInvestmentRelics =
    [
        typeof(BookOfFiveRings),
        typeof(DragonFruit),
        typeof(JuzuBracelet),
        typeof(MealTicket),
        typeof(Planisphere),
        typeof(WhiteBeastStatue),
        typeof(WhiteStar),
        typeof(FrozenEgg),
        typeof(MoltenEgg),
        typeof(ToxicEgg),
    ];

    private static readonly HashSet<Type> SellableInvestmentRelicSet =
        SellableInvestmentRelics.ToHashSet();

    public MerchantAct3GateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(40, 41, 1)]
    [InlineData(37, 38, 2)]
    public void ShopPull_OffersAllTenInvestmentRelicsBeforeThresholdAndNoneAtThreshold(
        int beforeThresholdFloor,
        int thresholdFloor,
        int playerCount)
    {
        (HashSet<Type> before, _) = ObserveExhaustiveShopOffers(beforeThresholdFloor, playerCount);
        (HashSet<Type> at, RelicGrabBag atBag) =
            ObserveExhaustiveShopOffers(thresholdFloor, playerCount);

        AssertGatedRelicsRemainInExpectedBuckets(atBag);
        AssertExpectedInvestmentRelics(before);
        Assert.Empty(at);
    }

    [Theory]
    [InlineData(38, 39, 40, 41, 1)]
    [InlineData(35, 36, 37, 38, 2)]
    public async Task RunDriver_CourierShopVisitsExpectedBoundaryFloorsAndExcludesAtThreshold(
        int beforeRunFloor,
        int atRunFloor,
        int expectedBeforeShopFloor,
        int expectedAtShopFloor,
        int playerCount)
    {
        (_, HashSet<int> beforeFloors) =
            await ObserveRunDriverRestockOffers(beforeRunFloor, playerCount, "before");
        (HashSet<Type> at, HashSet<int> atFloors) =
            await ObserveRunDriverRestockOffers(atRunFloor, playerCount, "at");

        Assert.Equal([expectedBeforeShopFloor], beforeFloors);
        Assert.Empty(at);
        Assert.Equal([expectedAtShopFloor], atFloors);
    }

    private static (HashSet<Type> Relics, RelicGrabBag Bag)
        ObserveExhaustiveShopOffers(int totalFloor, int playerCount)
    {
        var observed = new HashSet<Type>();
        RunState runState = CreateRunAtFloor(
            $"merchant-act3-initial-{totalFloor}-{playerCount}",
            totalFloor,
            playerCount);
        foreach (RelicRarity rarity in new[]
                 { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop })
        {
            while (runState.Players[0].RelicGrabBag.PullForShop(rarity, runState) is RelicModel relic)
            {
                if (SellableInvestmentRelicSet.Contains(relic.GetType()))
                {
                    observed.Add(relic.GetType());
                }
            }
        }

        return (observed, runState.Players[0].RelicGrabBag);
    }

    private static async Task<(HashSet<Type> Relics, HashSet<int> Floors)>
        ObserveRunDriverRestockOffers(int floorBeforeRun, int playerCount, string phase)
    {
        var observedRelics = new HashSet<Type>();
        var observedFloors = new HashSet<int>();
        for (int seedIndex = 0; seedIndex < RestockSeedCount; seedIndex++)
        {
            RunState runState = CreateRunAtFloor(
                $"merchant-act3-restock-{phase}-{seedIndex}",
                floorBeforeRun,
                playerCount);
            Player player = runState.Players[0];
            player.Gold = 999_999;
            await RelicCmd.Obtain(ModelDb.Relic<TheCourier>(), player);
            runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
            runState.Map.StartingMapPoint.Children
                .OrderBy(point => point.coord.col)
                .First()
                .PointType = MapPointType.Shop;
            var decisions = new RestockProbeDecisionSource(RestockPurchasesPerRun);
            var driver = new RunDriver(runState, decisions);

            RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

            Assert.Equal(RestockPurchasesPerRun, decisions.PurchasesMade);
            Assert.Equal(1, result.FloorsVisited);
            observedRelics.UnionWith(decisions.ObservedInvestmentRelics);
            observedFloors.UnionWith(decisions.ObservedTotalFloors);
        }

        return (observedRelics, observedFloors);
    }

    private static RunState CreateRunAtFloor(string seed, int totalFloor, int playerCount)
    {
        var runState = new RunState(seed, new Overgrowth());
        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
            runState.AddPlayer(player);
        }
        for (int floor = 0; floor < totalFloor; floor++)
        {
            runState.AddVisitedMapCoord(new MapCoord(1_000 + floor, -1_000));
        }

        return runState;
    }

    private static void AddInvestmentRelics(
        MerchantInventory inventory,
        HashSet<Type> observed) =>
        observed.UnionWith(inventory.Relics
            .Select(entry => entry.Relic.GetType())
            .Where(SellableInvestmentRelicSet.Contains));

    private static void AssertGatedRelicsRemainInExpectedBuckets(RelicGrabBag bag)
    {
        var expectedByRarity = new Dictionary<RelicRarity, Type[]>
        {
            [RelicRarity.Common] =
                [typeof(BookOfFiveRings), typeof(JuzuBracelet), typeof(MealTicket)],
            [RelicRarity.Uncommon] = [typeof(Planisphere)],
            [RelicRarity.Rare] = [typeof(FrozenEgg), typeof(MoltenEgg), typeof(ToxicEgg), typeof(WhiteBeastStatue), typeof(WhiteStar)],
            [RelicRarity.Shop] = [typeof(DragonFruit)],
        };

        foreach ((RelicRarity rarity, Type[] expectedTypes) in expectedByRarity)
        {
            var actualTypes = new HashSet<Type>();
            while (bag.PullFromFront(rarity) is RelicModel relic)
            {
                if (SellableInvestmentRelicSet.Contains(relic.GetType()))
                {
                    actualTypes.Add(relic.GetType());
                }
            }

            Assert.Equal(
                expectedTypes.Select(type => type.Name).Order(),
                actualTypes.Select(type => type.Name).Order());
        }
    }

    private static void AssertExpectedInvestmentRelics(HashSet<Type> actual) =>
        Assert.Equal(
            SellableInvestmentRelics.Select(type => type.Name).Order(),
            actual.Select(type => type.Name).Order());

    private sealed class RestockProbeDecisionSource(int purchases) : IRunDecisionSource
    {
        private int _purchasesRemaining = purchases;
        public int PurchasesMade { get; private set; }

        public HashSet<Type> ObservedInvestmentRelics { get; } = new();

        public HashSet<int> ObservedTotalFloors { get; } = new();

        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options.OrderBy(point => point.coord.col).First());

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
            Task.FromResult<RewardDecision>(new RewardDecision.Done());

        public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player)
        {
            AddInvestmentRelics(inventory, ObservedInvestmentRelics);
            ObservedTotalFloors.Add(player.RunState.TotalFloor);
            if (_purchasesRemaining-- <= 0)
            {
                return Task.FromResult<ShopDecision>(new ShopDecision.Leave());
            }

            PurchasesMade++;
            return Task.FromResult<ShopDecision>(new ShopDecision.BuyRelic(inventory.Relics[0]));
        }
    }
}
