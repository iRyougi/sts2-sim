using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

internal sealed class FirstChoiceDecisionSource : IRunDecisionSource
{
    public int RewardDecisionCount { get; private set; }

    public int DoneDecisionCount { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(p => p.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        Player player = state.Players[0];
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards)
        {
            if (card.CanPlay(out _))
            {
                Creature? target = card.TargetType == TargetType.AnyEnemy
                    ? state.HittableEnemies.FirstOrDefault()
                    : null;
                if (card.TargetType == TargetType.AnyEnemy && target is null) continue;
                return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, target));
            }
        }

        return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
    {
        RewardDecisionCount++;
        RewardDecision decision = RewardDecisionPolicy.Choose(rewards);
        if (decision is RewardDecision.Done)
        {
            DoneDecisionCount++;
        }

        return Task.FromResult(decision);
    }

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
        Task.FromResult(ShopDecisionPolicy.Choose(inventory, player));
}

internal sealed class PotionFirstDecisionSource : IRunDecisionSource
{
    public int PotionDecisionCount { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(point => point.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        Player player = state.Players[0];
        PotionModel? potion = player.PotionSlots.OfType<StrengthPotion>().FirstOrDefault();
        if (potion is not null)
        {
            PotionDecisionCount++;
            return Task.FromResult<CombatDecision>(
                new CombatDecision.UsePotion(potion, player.Creature));
        }

        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards)
        {
            if (!card.CanPlay(out _))
            {
                continue;
            }

            Creature? target = card.TargetType == TargetType.AnyEnemy
                ? state.HittableEnemies.FirstOrDefault()
                : null;
            return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, target));
        }

        return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }
}

internal sealed class InvalidMapDecisionSource : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(new MapPoint(-1, -1) { PointType = MapPointType.Boss });

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        Task.FromResult(RewardDecisionPolicy.Choose(rewards));

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
        Task.FromResult(ShopDecisionPolicy.Choose(inventory, player));
}

internal sealed class InvalidCardDecisionSource : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(p => p.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        CardModel card = state.Players[0].PlayerCombatState!.DrawPile.Cards.First();
        return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, Target: null));
    }

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        Task.FromResult(RewardDecisionPolicy.Choose(rewards));

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
        Task.FromResult(ShopDecisionPolicy.Choose(inventory, player));
}

internal sealed class InvalidTargetDecisionSource : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(p => p.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        CardModel card = state.Players[0].PlayerCombatState!.Hand.Cards
            .First(card => card.TargetType == TargetType.AnyEnemy && card.CanPlay(out _));
        return Task.FromResult<CombatDecision>(
            new CombatDecision.PlayCard(card, state.Players[0].Creature));
    }

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        Task.FromResult(RewardDecisionPolicy.Choose(rewards));

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
        Task.FromResult(ShopDecisionPolicy.Choose(inventory, player));
}

internal sealed class RepeatedGoldDecisionSource : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(p => p.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        Player player = state.Players[0];
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards)
        {
            if (card.CanPlay(out _))
            {
                Creature? target = card.TargetType == TargetType.AnyEnemy
                    ? state.HittableEnemies.FirstOrDefault()
                    : null;
                if (card.TargetType == TargetType.AnyEnemy && target is null) continue;
                return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, target));
            }
        }

        return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        Task.FromResult<RewardDecision>(new RewardDecision.TakeGold());

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
        Task.FromResult(ShopDecisionPolicy.Choose(inventory, player));
}

internal sealed class DoneRewardDecisionSource : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(p => p.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        Player player = state.Players[0];
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards)
        {
            if (card.CanPlay(out _))
            {
                Creature? target = card.TargetType == TargetType.AnyEnemy
                    ? state.HittableEnemies.FirstOrDefault()
                    : null;
                if (card.TargetType == TargetType.AnyEnemy && target is null) continue;
                return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, target));
            }
        }

        return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        Task.FromResult<RewardDecision>(new RewardDecision.Done());

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
        Task.FromResult(ShopDecisionPolicy.Choose(inventory, player));
}

internal static class RewardDecisionPolicy
{
    public static RewardDecision Choose(RewardsSet rewards) =>
        RewardDecisionClassifier.ChooseDefault(rewards);
}

internal static class ShopDecisionPolicy
{
    public static ShopDecision Choose(MerchantInventory inventory, Player player) => new ShopDecision.Leave();
}

internal sealed class ScriptedShopDecisionSource : IRunDecisionSource
{
    private readonly Queue<Func<MerchantInventory, Player, ShopDecision>> _shopDecisions;

    public ScriptedShopDecisionSource(params Func<MerchantInventory, Player, ShopDecision>[] shopDecisions)
    {
        _shopDecisions = new Queue<Func<MerchantInventory, Player, ShopDecision>>(shopDecisions);
    }

    public Player? ShopPlayer { get; private set; }

    public int ShopDecisionCount { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(p => p.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        Task.FromResult(RewardDecisionPolicy.Choose(rewards));

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player)
    {
        ShopDecisionCount++;
        ShopPlayer = player;
        return Task.FromResult(_shopDecisions.Dequeue()(inventory, player));
    }
}


internal sealed class ExhaustingShopDecisionSource : IRunDecisionSource
{
    public int ShopDecisionCount { get; private set; }

    public int MaximumPurchases { get; private set; }

    public MerchantInventory? Inventory { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(p => p.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        Task.FromResult(RewardDecisionPolicy.Choose(rewards));

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player)
    {
        ShopDecisionCount++;
        Inventory = inventory;
        MaximumPurchases = inventory.Cards.Count + inventory.Relics.Count + inventory.Potions.Count + 1;

        MerchantCardEntry? card = inventory.Cards.FirstOrDefault(entry => !entry.Purchased);
        if (card is not null)
        {
            return Task.FromResult<ShopDecision>(new ShopDecision.BuyCard(card));
        }

        MerchantRelicEntry? relic = inventory.Relics.FirstOrDefault(entry => !entry.Purchased);
        if (relic is not null)
        {
            return Task.FromResult<ShopDecision>(new ShopDecision.BuyRelic(relic));
        }

        MerchantPotionEntry? potion = inventory.Potions.FirstOrDefault(entry => !entry.Purchased);
        if (potion is not null)
        {
            PotionModel? occupiedSlot = player.PotionSlots.FirstOrDefault(slot => slot is not null);
            if (!player.PotionSlots.Contains(null) && occupiedSlot is not null)
            {
                player.RemovePotionInternal(occupiedSlot);
            }
            return Task.FromResult<ShopDecision>(new ShopDecision.BuyPotion(potion));
        }

        CardModel cardToRemove = player.Deck.Cards[0];
        return Task.FromResult<ShopDecision>(new ShopDecision.BuyCardRemoval(cardToRemove));
    }
}

internal sealed class EventDecisionSource : IRunDecisionSource
{
    private readonly Func<IReadOnlyList<EventOption>, EventOption> _chooseEventOption;

    public EventDecisionSource(Func<IReadOnlyList<EventOption>, EventOption> chooseEventOption)
    {
        _chooseEventOption = chooseEventOption;
    }

    public int EventDecisionCount { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(point => point.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

    public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options)
    {
        EventDecisionCount++;
        return Task.FromResult(_chooseEventOption(options));
    }
}

public class RunDriverTests : IDisposable
{
    public RunDriverTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    private static MapPoint PickFirstByCoord(IEnumerable<MapPoint> points) =>
        points.OrderBy(p => p.coord.col).First();

    private static RunState NewRunState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }

    private static void MakeFirstReachableRoomAShop(RunState runState)
    {
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Shop;
    }
    [Fact]

    public async Task RunAsync_MatchesRunEngine_ForSameSeedAndEquivalentCurrentPolicy()
    {
        RunState runStateA = NewRunState("run-driver-equivalence");
        var engine = new RunEngine(runStateA, PickFirstByCoord);
        var engineRooms = new List<(MapCoord Coord, RoomType RoomType)>();
        engine.OnRoomResolved += (point, roomType) => engineRooms.Add((point.coord, roomType));
        RunEngine.Result engineResult = await engine.RunAsync(maxFloors: 200);

        RunState runStateB = NewRunState("run-driver-equivalence");
        var decisionSource = new FirstChoiceDecisionSource();
        var driver = new RunDriver(runStateB, decisionSource);
        var driverRooms = new List<(MapCoord Coord, RoomType RoomType)>();
        driver.OnRoomResolved += (point, roomType) => driverRooms.Add((point.coord, roomType));
        RunDriver.Result driverResult = await driver.RunAsync(maxFloors: 200);

        Assert.Equal(engineResult.Won, driverResult.Won);
        Assert.Equal(engineResult.ReachedBoss, driverResult.ReachedBoss);
        Assert.Equal(engineResult.FloorsVisited, driverResult.FloorsVisited);
        Assert.Equal(engineResult.FinalPlayerHp, driverResult.FinalPlayerHp);
        Assert.Equal(engineRooms, driverRooms);
        Assert.True(decisionSource.RewardDecisionCount > 0);
    }

    [Fact]
    public async Task RunAsync_ReturnsLossImmediately_WhenPlayerAlreadyDead()
    {
        RunState runState = NewRunState("run-driver-dead");
        Player player = runState.Players[0];
        player.Creature.LoseHpInternal(player.Creature.MaxHp, default);

        var driver = new RunDriver(runState, new FirstChoiceDecisionSource());
        RunDriver.Result result = await driver.RunAsync(maxFloors: 200);

        Assert.False(result.Won);
    }

    [Fact]
    public async Task RunAsync_RaisesOnRoomResolved_OncePerFloor()
    {
        RunState runState = NewRunState("run-driver-events");
        var driver = new RunDriver(runState, new FirstChoiceDecisionSource());
        int raisedCount = 0;
        driver.OnRoomResolved += (_, _) => raisedCount++;

        RunDriver.Result result = await driver.RunAsync(maxFloors: 200);

        Assert.Equal(result.FloorsVisited, raisedCount);
    }

    [Fact]
    public async Task RunAsync_RejectsMapPointOutsideCurrentOptions()
    {
        RunState runState = NewRunState("run-driver-invalid-map");
        var driver = new RunDriver(runState, new InvalidMapDecisionSource());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 200));

        Assert.Contains("current map options", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsCardOutsideCurrentHand()
    {
        RunState runState = NewRunState("run-driver-invalid-card");
        var driver = new RunDriver(runState, new InvalidCardDecisionSource());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 200));

        Assert.Contains("current hand", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsEnemyCardTargetThatIsNotHittable()
    {
        RunState runState = NewRunState("run-driver-invalid-target");
        var driver = new RunDriver(runState, new InvalidTargetDecisionSource());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 200));

        Assert.Contains("hittable enemy", exception.Message);
    }

    [Fact]
    public async Task RunAsync_UsePotionDecision_ExecutesPotionThroughCombatLifecycle()
    {
        RunState runState = NewRunState("run-driver-use-potion");
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Monster;
        Player player = runState.Players[0];
        PotionModel potion = player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        var decisionSource = new PotionFirstDecisionSource();
        var driver = new RunDriver(runState, decisionSource);

        RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

        Assert.Equal(1, result.FloorsVisited);
        Assert.Equal(1, decisionSource.PotionDecisionCount);
        Assert.DoesNotContain(player.PotionSlots, slot => ReferenceEquals(slot, potion));
    }

    [Fact]
    public async Task RunAsync_RejectsRewardActionAfterThatRewardWasResolved()
    {
        RunState runState = NewRunState("run-driver-equivalence");
        var driver = new RunDriver(runState, new RepeatedGoldDecisionSource());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 200));

        Assert.Contains("Gold reward has already been resolved", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsDoneBeforeAllRewardsAreResolved()
    {
        RunState runState = NewRunState("run-driver-equivalence");
        var driver = new RunDriver(runState, new DoneRewardDecisionSource());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 200));

        Assert.Contains("unresolved rewards", exception.Message);
    }

    [Fact]
    public async Task RunAsync_AcceptsDoneAfterAllRewardsAreResolved()
    {
        RunState runState = NewRunState("run-driver-equivalence");
        var decisionSource = new FirstChoiceDecisionSource();
        var driver = new RunDriver(runState, decisionSource);

        _ = await driver.RunAsync(maxFloors: 200);

        Assert.True(decisionSource.DoneDecisionCount > 0);
    }

    [Fact]
    public async Task RunAsync_OffersBoundPlayerToShopDecisionSource_AndLeavesWhenRequested()
    {
        RunState runState = NewRunState("run-driver-shop-leave");
        MakeFirstReachableRoomAShop(runState);
        var decisionSource = new ScriptedShopDecisionSource(static (_, _) => new ShopDecision.Leave());
        var driver = new RunDriver(runState, decisionSource);

        RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

        Assert.Equal(1, result.FloorsVisited);
        Assert.Same(runState.Players[0], decisionSource.ShopPlayer);
    }

    [Fact]
    public async Task RunAsync_RejectsShopEntryOutsideCurrentInventory()
    {
        RunState runState = NewRunState("run-driver-shop-foreign");
        MakeFirstReachableRoomAShop(runState);
        var decisionSource = new ScriptedShopDecisionSource((inventory, _) =>
            new ShopDecision.BuyRelic(new MerchantRelicEntry(inventory.Relics[0].Relic, inventory.Relics[0].Price)));
        var driver = new RunDriver(runState, decisionSource);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        Assert.Contains("current merchant inventory", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsStaleShopEntry()
    {
        RunState runState = NewRunState("run-driver-shop-stale");
        Player player = runState.Players[0];
        MerchantRelicEntry staleEntry = MerchantInventory.Generate(player).Relics[0];
        MakeFirstReachableRoomAShop(runState);
        var decisionSource = new ScriptedShopDecisionSource((_, _) => new ShopDecision.BuyRelic(staleEntry));
        var driver = new RunDriver(runState, decisionSource);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        Assert.Contains("current merchant inventory", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsSoldShopEntry()
    {
        RunState runState = NewRunState("run-driver-shop-sold");
        runState.Players[0].Gold = 99999;
        MakeFirstReachableRoomAShop(runState);
        var decisionSource = new ScriptedShopDecisionSource(
            static (inventory, _) => new ShopDecision.BuyRelic(inventory.Relics[0]),
            static (inventory, _) => new ShopDecision.BuyRelic(inventory.Relics[0]));
        var driver = new RunDriver(runState, decisionSource);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        Assert.Contains("already been purchased", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsUnaffordableShopEntry()
    {
        RunState runState = NewRunState("run-driver-shop-unaffordable");
        runState.Players[0].Gold = 0;
        MakeFirstReachableRoomAShop(runState);
        var decisionSource = new ScriptedShopDecisionSource(
            static (inventory, _) => new ShopDecision.BuyRelic(inventory.Relics[0]));
        var driver = new RunDriver(runState, decisionSource);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        Assert.Contains("Not enough gold", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsShopRemovalOfCardOutsidePlayerDeck()
    {
        RunState runState = NewRunState("run-driver-shop-removal");
        runState.Players[0].Gold = 99999;
        MakeFirstReachableRoomAShop(runState);
        var decisionSource = new ScriptedShopDecisionSource(
            static (_, player) => new ShopDecision.BuyCardRemoval((CardModel)player.Deck.Cards[0].MutableClone()));
        var driver = new RunDriver(runState, decisionSource);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        Assert.Contains("player's deck", exception.Message);
    }

    [Fact]
    public async Task RunAsync_BuysPotionThenRequeriesBeforeLeaving()
    {
        RunState runState = NewRunState("run-driver-shop-buy-potion");
        Player player = runState.Players[0];
        player.Gold = 99999;
        MakeFirstReachableRoomAShop(runState);
        MerchantPotionEntry? purchasedEntry = null;
        int goldBefore = player.Gold;
        var decisionSource = new ScriptedShopDecisionSource(
            (inventory, _) =>
            {
                purchasedEntry = inventory.Potions[0];
                return new ShopDecision.BuyPotion(purchasedEntry);
            },
            static (_, _) => new ShopDecision.Leave());
        var driver = new RunDriver(runState, decisionSource);

        _ = await driver.RunAsync(maxFloors: 1);

        MerchantPotionEntry entry = Assert.IsType<MerchantPotionEntry>(purchasedEntry);
        Assert.Equal(goldBefore - entry.Price, player.Gold);
        Assert.True(entry.Purchased);
        Assert.Same(player, entry.Potion.Owner);
        Assert.Contains(player.PotionSlots, potion => ReferenceEquals(entry.Potion, potion));
        Assert.Equal(2, decisionSource.ShopDecisionCount);
    }

    [Fact]
    public async Task RunAsync_RemovesDeckCardThenRequeriesBeforeLeaving()
    {
        RunState runState = NewRunState("run-driver-shop-remove-card");
        Player player = runState.Players[0];
        player.Gold = 99999;
        CardModel cardToRemove = player.Deck.Cards[0];
        int deckCountBefore = player.Deck.Cards.Count;
        int goldBefore = player.Gold;
        MerchantCardRemovalEntry? removalEntry = null;
        MakeFirstReachableRoomAShop(runState);
        var decisionSource = new ScriptedShopDecisionSource(
            (inventory, _) =>
            {
                removalEntry = inventory.CardRemoval;
                return new ShopDecision.BuyCardRemoval(cardToRemove);
            },
            static (_, _) => new ShopDecision.Leave());
        var driver = new RunDriver(runState, decisionSource);

        _ = await driver.RunAsync(maxFloors: 1);

        MerchantCardRemovalEntry entry = Assert.IsType<MerchantCardRemovalEntry>(removalEntry);
        Assert.Equal(goldBefore - entry.Price, player.Gold);
        Assert.DoesNotContain(player.Deck.Cards, deckCard => ReferenceEquals(cardToRemove, deckCard));
        Assert.Equal(deckCountBefore - 1, player.Deck.Cards.Count);
        Assert.Equal(1, player.CardRemovalsUsed);
        Assert.True(entry.Purchased);
        Assert.Equal(2, decisionSource.ShopDecisionCount);
    }

    [Fact]
    public async Task RunAsync_RejectsExtraShopActionAfterEveryAvailableEntryWasPurchased()
    {
        RunState runState = NewRunState("run-driver-shop-action-cap");
        Player player = runState.Players[0];
        player.Gold = 99999;
        MakeFirstReachableRoomAShop(runState);
        var decisionSource = new ExhaustingShopDecisionSource();
        var driver = new RunDriver(runState, decisionSource);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        MerchantInventory inventory = Assert.IsType<MerchantInventory>(decisionSource.Inventory);
        Assert.Contains("did not leave", exception.Message);
        Assert.All(inventory.Cards, entry => Assert.True(entry.Purchased));
        Assert.All(inventory.Relics, entry => Assert.True(entry.Purchased));
        Assert.All(inventory.Potions, entry => Assert.True(entry.Purchased));
        Assert.True(inventory.CardRemoval.Purchased);
        Assert.Equal(decisionSource.MaximumPurchases + 1, decisionSource.ShopDecisionCount);
    }
    private static void MakeFirstReachableRoomAnEvent(RunState runState)
    {
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Unknown;
    }

    [Fact]
    public async Task RunAsync_DrivesSelectedAct1EventToCompletionUsingDecisionSourceOption()
    {
        RunState runState = NewRunState("room-factory-b");
        MakeFirstReachableRoomAnEvent(runState);
        var decisionSource = new EventDecisionSource(options => options[1]);
        var driver = new RunDriver(runState, decisionSource);

        RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

        Assert.Equal(1, result.FloorsVisited);
        Assert.Equal(1, decisionSource.EventDecisionCount);
    }

    [Fact]
    public async Task RunAsync_RejectsForeignEventOption()
    {
        RunState runState = NewRunState("room-factory-b");
        MakeFirstReachableRoomAnEvent(runState);
        var decisionSource = new EventDecisionSource(options =>
            new EventOption(options[0].Key, () => Task.CompletedTask));
        var driver = new RunDriver(runState, decisionSource);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        Assert.Contains("current event options", exception.Message);
    }
}
