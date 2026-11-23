using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Reporting;

[Collection("ModelDb")]
public sealed class NonCombatFloorDetailTests : IDisposable
{
    public NonCombatFloorDetailTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunEngine_RestSite_RecordsSuccessfulSmithAndActualUpgradedCardId()
    {
        RunState runState = NewTravelRun("detail-rest-engine-smith", MapPointType.RestSite);
        CardModel expectedCard = runState.Players[0].Deck.Cards.First(card => card.IsUpgradable);

        FloorEntry floor = await RunEngineFloorAsync(runState);

        var detail = Assert.IsType<RestSiteFloorDetail>(floor.Detail);
        Assert.Equal("Smith", detail.Decision);
        Assert.Null(detail.HealAmount);
        Assert.Equal(expectedCard.Id.ToString(), detail.UpgradedCard);
        Assert.True(expectedCard.IsUpgraded);
    }

    [Fact]
    public async Task RunDriver_RestSite_RecordsActualCappedHealRatherThanNominalHealAmount()
    {
        RunState runState = NewTravelRun("detail-rest-driver-heal", MapPointType.RestSite);
        Player player = runState.Players[0];
        player.Creature.LoseHpInternal(5m, default(ValueProp));
        int hpBefore = player.Creature.CurrentHp;
        var decisions = new ConfigurableDecisionSource
        {
            RestChoice = _ => new RestSiteDecision.Heal(),
        };

        FloorEntry floor = await RunDriverFloorAsync(runState, decisions);

        var detail = Assert.IsType<RestSiteFloorDetail>(floor.Detail);
        Assert.Equal("Heal", detail.Decision);
        Assert.Equal(5, detail.HealAmount);
        Assert.Null(detail.UpgradedCard);
        Assert.Equal(hpBefore + 5, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task RunEngine_RestSite_RecordsTruthfulHatchChoiceWithoutHealOrUpgradeFields()
    {
        RunState runState = NewTravelRun("detail-rest-engine-hatch", MapPointType.RestSite);
        Player player = runState.Players[0];
        AddDeckEgg(player);

        FloorEntry floor = await RunEngineFloorAsync(runState);

        var detail = Assert.IsType<RestSiteFloorDetail>(floor.Detail);
        Assert.Equal("Hatch", detail.Decision);
        Assert.Null(detail.HealAmount);
        Assert.Null(detail.UpgradedCard);
        Assert.Single(player.Relics.OfType<Byrdpip>());
    }

    [Fact]
    public async Task RunEngine_Shop_RecordsEmptyPurchasesBecauseItsPolicyOnlyLeaves()
    {
        RunState runState = NewTravelRun("detail-shop-engine-empty", MapPointType.Shop);

        FloorEntry floor = await RunEngineFloorAsync(runState);

        var detail = Assert.IsType<ShopFloorDetail>(floor.Detail);
        Assert.Empty(detail.Purchases);
    }

    [Fact]
    public async Task RunDriver_Shop_RecordsEverySuccessfulPurchaseIncludingCardRemoval()
    {
        RunState runState = NewTravelRun("detail-shop-driver-all", MapPointType.Shop);
        Player player = runState.Players[0];
        player.Gold = 100_000;
        int goldBefore = player.Gold;
        CardModel cardToRemove = player.Deck.Cards[0];
        var expected = new List<PurchaseRecord>();
        int decisionIndex = 0;
        var decisions = new ConfigurableDecisionSource
        {
            ShopChoice = (inventory, currentPlayer) => decisionIndex++ switch
            {
                0 => BuyCard(inventory.Cards[0], expected),
                1 => BuyRelic(inventory.Relics[0], expected),
                2 => BuyPotion(inventory.Potions[0], expected),
                3 => BuyCardRemoval(inventory.CardRemoval, cardToRemove, expected),
                _ => new ShopDecision.Leave(),
            },
        };

        FloorEntry floor = await RunDriverFloorAsync(runState, decisions);

        var detail = Assert.IsType<ShopFloorDetail>(floor.Detail);
        Assert.Equal(expected, detail.Purchases);
        Assert.Equal(["Card", "Relic", "Potion", "CardRemoval"], detail.Purchases.Select(p => p.Kind));
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, cardToRemove));
        Assert.Equal(goldBefore - expected.Sum(purchase => purchase.Price), player.Gold);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunDriver_Shop_PotionFailuresResumeWithoutRecordingPurchasesAndAreBounded(bool useSozu)
    {
        RunState runState = NewTravelRun("detail-shop-potion-failures", MapPointType.Shop);
        Player player = runState.Players[0];
        player.Gold = 100_000;
        Sozu? sozu = null;
        if (useSozu)
        {
            sozu = (Sozu)ModelDb.Relic<Sozu>().MutableClone();
            sozu.AssignOwner(player);
            player.AddRelicInternal(sozu);
        }
        else
        {
            for (int index = 0; index < player.MaxPotionCount; index++)
                player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        }
        int goldBefore = player.Gold;
        var slotsBefore = player.PotionSlots.ToArray();
        MerchantPotionEntry? shelf = null;
        PotionModel? potion = null;
        int price = 0;
        int decisionsTaken = 0;
        var decisions = new ConfigurableDecisionSource
        {
            ShopChoice = (inventory, currentPlayer) =>
            {
                if (decisionsTaken++ == 0)
                {
                    shelf = inventory.Potions[0];
                    potion = shelf.Potion;
                    price = shelf.Price;
                }
                else if (decisionsTaken <= 3)
                {
                    Assert.Equal(goldBefore, currentPlayer.Gold);
                    Assert.Equal(slotsBefore, currentPlayer.PotionSlots);
                    Assert.Same(shelf, inventory.Potions[0]);
                    Assert.Same(potion, shelf!.Potion);
                    Assert.False(shelf.Purchased);
                    Assert.Null(potion!.Owner);
                    if (decisionsTaken == 3)
                    {
                        if (sozu is not null) currentPlayer.RemoveRelicInternal(sozu);
                        else currentPlayer.RemovePotionInternal(currentPlayer.PotionSlots[0]!);
                    }
                }
                return decisionsTaken <= 3 ? new ShopDecision.BuyPotion(shelf!) : new ShopDecision.Leave();
            },
        };

        FloorEntry floor = await RunDriverFloorAsync(runState, decisions);

        var detail = Assert.IsType<ShopFloorDetail>(floor.Detail);
        PurchaseRecord record = Assert.Single(detail.Purchases);
        Assert.Equal(new PurchaseRecord("Potion", potion!.Id.ToString(), price), record);
        Assert.Equal(goldBefore - price, player.Gold);
        Assert.True(shelf!.Purchased);
        Assert.Contains(player.PotionSlots, item => ReferenceEquals(potion, item));
        Assert.Same(player, potion.Owner);
        Assert.Equal(4, decisionsTaken);

        // The same failure mode from a source that never leaves must terminate explicitly.
        RunState stuckRun = NewTravelRun("detail-shop-potion-failure-limit", MapPointType.Shop);
        Player stuckPlayer = stuckRun.Players[0];
        stuckPlayer.Gold = 100_000;
        for (int index = 0; index < stuckPlayer.MaxPotionCount; index++)
            stuckPlayer.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        int failures = 0;
        string? failedPotionId = null;
        var stuckSource = new ConfigurableDecisionSource
        {
            ShopChoice = (inventory, _) =>
            {
                failures++;
                failedPotionId = inventory.Potions[0].Potion.Id.ToString();
                return new ShopDecision.BuyPotion(inventory.Potions[0]);
            },
        };
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunDriverFloorAsync(stuckRun, stuckSource));
        Assert.Equal(33, failures);
        Assert.Contains("33", exception.Message);
        Assert.Contains(failedPotionId!, exception.Message);
        Assert.Contains("failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunDriver_Shop_DoesNotPublishRejectedPurchaseAsFloorDetail()
    {
        RunState runState = NewTravelRun("detail-shop-driver-rejected", MapPointType.Shop);
        var recorder = new DetailSpyRecorder();
        var decisions = new ConfigurableDecisionSource
        {
            ShopChoice = (inventory, _) =>
                new ShopDecision.BuyRelic(new MerchantRelicEntry(inventory.Relics[0].Relic, 0)),
        };
        var driver = new RunDriver(
            runState,
            decisions,
            createAncientEventRoom: null,
            recorder: recorder);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RunAsync(maxFloors: 1));

        Assert.Contains("current merchant inventory", exception.Message);
        Assert.Empty(recorder.Details);
    }

    [Fact]
    public async Task RunEngine_Treasure_RecordsActualGoldAndStackedCircletAward()
    {
        RunState runState = NewTravelRun("detail-treasure-engine-circlet", MapPointType.Treasure);
        Player player = runState.Players[0];
        foreach (RelicRarity rarity in new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare })
        {
            while (runState.SharedRelicGrabBag!.PullFromFront(rarity) is not null) { }
        }

        await RelicCmd.Obtain(ModelDb.Relic<Circlet>(), player);
        Circlet ownedCirclet = Assert.Single(player.Relics.OfType<Circlet>());
        int stackBefore = ownedCirclet.StackCount;
        int goldBefore = player.Gold;

        FloorEntry floor = await RunEngineFloorAsync(runState);

        var detail = Assert.IsType<TreasureFloorDetail>(floor.Detail);
        Assert.Equal(player.Gold - goldBefore, detail.GoldGained);
        Assert.InRange(detail.GoldGained, 42, 52);
        Assert.Equal(ModelDb.Relic<Circlet>().Id.ToString(), detail.RelicGained);
        Assert.Equal(stackBefore + 1, ownedCirclet.StackCount);
    }

    [Fact]
    public async Task RunDriver_Treasure_PreservesZeroAndNullWhenGenerationIsVetoed()
    {
        RunState runState = NewTravelRun("detail-treasure-driver-veto", MapPointType.Treasure);
        Player player = runState.Players[0];
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), player);
        int goldBefore = player.Gold;
        int relicCountBefore = player.Relics.Count;

        FloorEntry floor = await RunDriverFloorAsync(runState, new ConfigurableDecisionSource());

        var detail = Assert.IsType<TreasureFloorDetail>(floor.Detail);
        Assert.Equal(0, detail.GoldGained);
        Assert.Null(detail.RelicGained);
        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(relicCountBefore, player.Relics.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunPaths_Event_RecordActualModelOptionAndBeforeAfterEffects(bool useDriver)
    {
        // Isolate event-detail recording from seeded event selection with an explicit event queue.
        RunState runState = NewTravelRun("shared-detail-event-4", MapPointType.Unknown);
        _ = runState.CurrentAncientEventType;
        typeof(RunState).GetField("_eventSequence",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(runState, new List<Type> { typeof(Sts2Sim.Core.Models.Events.TabletOfTruth) });
        runState.Rng.MockRng(RunRngType.UnknownMapPoint, 0UL);

        FloorEntry floor = useDriver
            ? await RunDriverFloorAsync(runState, new ConfigurableDecisionSource())
            : await RunEngineFloorAsync(runState);

        var detail = Assert.IsType<EventFloorDetail>(floor.Detail);
        Assert.Equal("TabletOfTruth", detail.EventName);
        Assert.Equal("DECIPHER", detail.OptionChosen);
        Assert.Equal(
            "hp:75->1; max_hp:75->1; " +
            "deck_removed:[CARD.STRIKE_REGENT,CARD.STRIKE_REGENT,CARD.STRIKE_REGENT,CARD.STRIKE_REGENT," +
            "CARD.DEFEND_REGENT,CARD.DEFEND_REGENT,CARD.DEFEND_REGENT,CARD.DEFEND_REGENT," +
            "CARD.FALLING_STAR,CARD.VENERATE]; " +
            "deck_added:[CARD.STRIKE_REGENT(upgrade=1,enchantments=[]),CARD.STRIKE_REGENT(upgrade=1,enchantments=[])," +
            "CARD.STRIKE_REGENT(upgrade=1,enchantments=[]),CARD.STRIKE_REGENT(upgrade=1,enchantments=[])," +
            "CARD.DEFEND_REGENT(upgrade=1,enchantments=[]),CARD.DEFEND_REGENT(upgrade=1,enchantments=[])," +
            "CARD.DEFEND_REGENT(upgrade=1,enchantments=[]),CARD.DEFEND_REGENT(upgrade=1,enchantments=[])," +
            "CARD.FALLING_STAR(upgrade=1,enchantments=[]),CARD.VENERATE(upgrade=1,enchantments=[])]",
            detail.EffectsSummary);
        Assert.Equal(["RELIC.DIVINE_RIGHT"], floor.RelicsAfter);
        Assert.Equal(1, floor.HpAfter);
        Assert.Equal(1, floor.MaxHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunPaths_Ancient_RecordActualEventModelAndSuccessfulOption(bool useDriver)
    {
        RunState runState = NewAncientRun($"detail-ancient-{useDriver}");
        int goldBefore = runState.Players[0].Gold;
        var recorder = new RunRecorder();
        Func<RunState, AbstractRoom> createAncientRoom = _ => new EventRoom(
            () => (EventModel)new FixedAncientEvent().MutableClone());

        if (useDriver)
        {
            var driver = new RunDriver(
                runState,
                new ConfigurableDecisionSource(),
                createAncientRoom,
                recorder);
            RunDriver.Result result = await driver.RunAsync(maxFloors: 0);
            Assert.Equal(0, result.FloorsVisited);
        }
        else
        {
            var engine = new RunEngine(
                runState,
                PickFirstByCoord,
                createAncientRoom,
                recorder);
            RunEngine.Result result = await engine.RunAsync(maxFloors: 0);
            Assert.Equal(0, result.FloorsVisited);
        }

        FloorEntry floor = Assert.Single(recorder.BuildManifest().Floors);
        var detail = Assert.IsType<AncientFloorDetail>(floor.Detail);
        Assert.Equal("FixedAncientEvent", detail.EventName);
        Assert.Equal("TAKE_GOLD", detail.OptionChosen);
        Assert.Equal(goldBefore + 13, floor.GoldAfter);
    }

    private static async Task<FloorEntry> RunEngineFloorAsync(RunState runState)
    {
        var recorder = new RunRecorder();
        var engine = new RunEngine(
            runState,
            PickFirstByCoord,
            createAncientEventRoom: null,
            recorder: recorder);

        RunEngine.Result result = await engine.RunAsync(maxFloors: 1);

        Assert.Equal(1, result.FloorsVisited);
        return Assert.Single(recorder.BuildManifest().Floors);
    }

    private static async Task<FloorEntry> RunDriverFloorAsync(
        RunState runState,
        IRunDecisionSource decisionSource)
    {
        var recorder = new RunRecorder();
        var driver = new RunDriver(
            runState,
            decisionSource,
            createAncientEventRoom: null,
            recorder: recorder);

        RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

        Assert.Equal(1, result.FloorsVisited);
        return Assert.Single(recorder.BuildManifest().Floors);
    }

    private static RunState NewTravelRun(string seed, MapPointType firstFloorType)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        runState.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Regent>(), runState));
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = firstFloorType;
        return runState;
    }

    private static RunState NewAncientRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Ancient;
        runState.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Regent>(), runState));
        return runState;
    }

    private static MapPoint PickFirstByCoord(IEnumerable<MapPoint> points) =>
        points.OrderBy(point => point.coord.col).First();

    private static void AddDeckEgg(Player player)
    {
        var egg = (ByrdonisEgg)ModelDb.Card<ByrdonisEgg>().MutableClone();
        egg.AssignOwner(player);
        player.Deck.AddInternal(egg);
    }

    private static ShopDecision BuyCard(
        MerchantCardEntry entry,
        ICollection<PurchaseRecord> expected)
    {
        expected.Add(new PurchaseRecord("Card", entry.Card.Id.ToString(), entry.Price));
        return new ShopDecision.BuyCard(entry);
    }

    private static ShopDecision BuyRelic(
        MerchantRelicEntry entry,
        ICollection<PurchaseRecord> expected)
    {
        expected.Add(new PurchaseRecord("Relic", entry.Relic.Id.ToString(), entry.Price));
        return new ShopDecision.BuyRelic(entry);
    }

    private static ShopDecision BuyPotion(
        MerchantPotionEntry entry,
        ICollection<PurchaseRecord> expected)
    {
        expected.Add(new PurchaseRecord("Potion", entry.Potion.Id.ToString(), entry.Price));
        return new ShopDecision.BuyPotion(entry);
    }

    private static ShopDecision BuyCardRemoval(
        MerchantCardRemovalEntry entry,
        CardModel card,
        ICollection<PurchaseRecord> expected)
    {
        expected.Add(new PurchaseRecord("CardRemoval", card.Id.ToString(), entry.Price));
        return new ShopDecision.BuyCardRemoval(card);
    }

    private sealed class ConfigurableDecisionSource : IRunDecisionSource
    {
        public Func<MerchantInventory, Player, ShopDecision>? ShopChoice { get; init; }

        public Func<Player, RestSiteDecision>? RestChoice { get; init; }

        public Func<IReadOnlyList<EventOption>, EventOption>? EventChoice { get; init; }

        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(PickFirstByCoord(options));

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
            Task.FromResult(ShopChoice?.Invoke(inventory, player) ?? new ShopDecision.Leave());

        public Task<RestSiteDecision> ChooseRestSiteActionAsync(
            Player player,
            IReadOnlyList<RestSiteDecision> candidates)
        {
            if (RestChoice is not null)
            {
                return Task.FromResult(RestChoice(player));
            }

            return Task.FromResult(RestSiteDecisionPolicy.ChooseDefault(candidates));
        }

        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
            Task.FromResult(EventChoice?.Invoke(options) ?? options[0]);
    }

    private sealed class FixedAncientEvent : AncientEventModel
    {
        public override IReadOnlyList<RelicModel> AllPossibleOptions => [];

        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [
            new EventOption("TAKE_GOLD", async () =>
            {
                await PlayerCmd.GainGold(13m, Owner);
                Finish();
            }),
        ];
    }

    private sealed class DetailSpyRecorder : IRunRecorder
    {
        public List<FloorDetail> Details { get; } = [];

        public IReadOnlyDictionary<string, CombatLog> CombatLogs =>
            throw new NotSupportedException();

        public void BeginRun(RunState runState)
        {
        }

        public void EnterFloor(MapPoint point, RoomType roomType)
        {
        }

        public void RecordFloorDetail(FloorDetail detail) => Details.Add(detail);

        public string BeginCombat(
            RunState runState,
            RoomType encounterType,
            string encounterName,
            CombatState combatState) => throw new NotSupportedException();

        public void RecordTurnStart(CombatState combatState) => throw new NotSupportedException();

        public void RecordDraw(CardModel card) => throw new NotSupportedException();

        public void RecordCardPlay(
            CardPlay cardPlay,
            PlayerSnapshot before,
            PlayerSnapshot after,
            IReadOnlyList<EnemySnapshot> enemiesBefore,
            IReadOnlyList<EnemySnapshot> enemiesAfter) => throw new NotSupportedException();

        public void RecordPotionUse(
            PotionModel potion,
            Creature? target,
            PlayerSnapshot before,
            PlayerSnapshot after,
            IReadOnlyList<EnemySnapshot> enemiesBefore,
            IReadOnlyList<EnemySnapshot> enemiesAfter) => throw new NotSupportedException();

        public void RecordEnemyAction(
            Creature source,
            string moveId,
            IReadOnlyList<ActionSnapshotSegment> segments) => throw new NotSupportedException();

        public void RecordEndTurn(
            PlayerSnapshot finalPlayer,
            IReadOnlyList<EnemySnapshot> finalEnemies) => throw new NotSupportedException();

        public void EndCombat(
            bool victory,
            PlayerSnapshot finalPlayer,
            IReadOnlyList<EnemySnapshot> finalEnemies,
            CombatRewards rewards) => throw new NotSupportedException();

        public void ExitFloor()
        {
        }

        public void EndRun(
            bool won,
            int floorsVisited,
            int finalHp,
            bool reachedBoss = false,
            bool? survived = null,
            bool truncated = false,
            int actsCleared = 0)
        {
        }

        public RunManifest BuildManifest() => throw new NotSupportedException();
    }
}
