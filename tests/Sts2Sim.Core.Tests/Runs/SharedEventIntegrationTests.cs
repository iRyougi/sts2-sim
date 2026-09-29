using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class SharedEventIntegrationTests : IDisposable
{
    public SharedEventIntegrationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void SharedPool_PreservesSourceOrderWithoutDuplicates()
    {
        Assert.Equal(new[]
        {
            typeof(BrainLeech), typeof(CrystalSphere), typeof(DollRoom), typeof(FakeMerchant),
            typeof(PotionCourier), typeof(RanwidTheElder), typeof(RelicTrader), typeof(RoomFullOfCheese),
            typeof(SelfHelpBook), typeof(SlipperyBridge), typeof(StoneOfAllTime), typeof(Symbiote),
            typeof(TeaMaster), typeof(TheFutureOfPotions), typeof(TheLegendsWereTrue), typeof(ThisOrThat),
            typeof(WarHistorianRepy), typeof(WelcomeToWongos),
        }, SharedEventPool.All);
        Assert.Equal(18, SharedEventPool.All.Distinct().Count());
    }

    [Theory]
    [InlineData(typeof(Overgrowth), 13, 31)]
    [InlineData(typeof(Hive), 10, 28)]
    [InlineData(typeof(Glory), 7, 25)]
    public void EffectivePool_PutsOwnEventsBeforeShared(Type actType, int ownCount, int total)
    {
        var act = (ActDefinition)Activator.CreateInstance(actType)!;
        Assert.Equal(ownCount, act.EventPool.Count);
        Assert.Equal(total, act.EffectiveEventPool.Count);
        Assert.Equal(total, act.EffectiveEventPool.Distinct().Count());
        Assert.Equal(act.EventPool, act.EffectiveEventPool.Take(ownCount));
        Assert.Equal(SharedEventPool.All, act.EffectiveEventPool.Skip(ownCount));
    }

    [Fact]
    public void NaturalSelection_NeverFallsBackToRepyAfterExhaustion()
    {
        var run = NewRun();
        run.AdvanceToNextAct();
        run.AdvanceToNextAct();
        int counter = run.Rng.UpFront.Counter;
        Type[] events = Enumerable.Range(0, 100).Select(_ => run.PullNextEvent()).ToArray();
        Assert.DoesNotContain(typeof(WarHistorianRepy), events);
        Assert.Contains(typeof(ThisOrThat), events);
        Assert.Equal(counter, run.Rng.UpFront.Counter);
    }

    [Fact]
    public async Task LanternKey_Act3UnknownBecomesRepyAndConsumesKey()
    {
        var run = NewRun();
        Player player = run.Players[0];
        var key = (CardModel)ModelDb.Card<LanternKey>().MutableClone();
        key.AssignOwner(player);
        await CardPileCmd.AddToDeck(key);
        run.AdvanceToNextAct();
        Assert.NotEqual(typeof(WarHistorianRepy), run.PullNextEvent());
        run.AdvanceToNextAct();
        var room = Assert.IsType<EventRoom>(RoomFactory.CreateRoom(run, RoomType.Event));
        run.PushRoom(room);
        await room.Enter(run);
        Assert.IsType<WarHistorianRepy>(room.Event);
        await new RunEngine(run, PickFirst).DriveEventAsync(room);
        await room.Exit(run);
        run.PopCurrentRoom();
        Assert.True(run.FreedRepy);
        Assert.DoesNotContain(player.Deck.Cards, c => c is LanternKey);
        Assert.Contains(player.Relics, r => r is HistoryCourse);
        Assert.NotEqual(typeof(WarHistorianRepy), run.PullNextEvent());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrystalSphere_CustomActionsExhaustDivinationsAndDrainRewards(bool driver)
    {
        var run = NewRun();
        var sphere = (CrystalSphere)ModelDb.Event<CrystalSphere>().MutableClone();
        await RunEvent(driver, run, sphere);
        Assert.True(sphere.IsFinished);
        Assert.Empty(sphere.CurrentOptions);
        Assert.Equal(0, sphere.Game!.DivinationCount);
        Assert.False(sphere.HasPendingRewardOffers);
        Assert.All(sphere.Game.Rewards, reward => Assert.True(reward.IsResolved));
        Assert.Null(run.CurrentRoom);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FakeMerchant_CustomPurchaseThenLeaveUsesZeroOrdinaryOptions(bool driver)
    {
        var run = NewRun();
        var merchant = (FakeMerchant)ModelDb.Event<FakeMerchant>().MutableClone();
        MerchantRelicEntry? bought = null;
        int gold = run.Players[0].Gold;
        await RunEvent(driver, run, merchant, e =>
        {
            Assert.Empty(e.CurrentOptions);
            if (bought is null)
            {
                bought = merchant.Inventory.Relics[0];
                return new CustomEventDecision.BuyRelic(bought);
            }
            return new CustomEventDecision.Leave();
        });
        Assert.True(merchant.IsFinished);
        Assert.True(bought!.Purchased);
        Assert.Contains(bought.Relic, run.Players[0].Relics);
        Assert.Equal(gold - bought.Price, run.Players[0].Gold);
        Assert.Null(run.CurrentRoom);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task FakeMerchant_ForcedCombatHonorsNormalAndUnboughtRewards(bool driver, bool victory)
    {
        var run = NewRun();
        Player player = run.Players[0];
        if (!victory)
        {
            player.Creature.LoseHpInternal(player.Creature.CurrentHp - 1, default);
            foreach (CardModel card in player.Deck.Cards.ToArray()) CardPileCmd.Remove(card);
        }
        var potion = player.AddPotionInternal(ModelDb.Potion<FoulPotion>());
        var merchant = (FakeMerchant)ModelDb.Event<FakeMerchant>().MutableClone();
        MerchantRelicEntry? bought = null;
        int originalDeck = player.Deck.Cards.Count;
        int originalGold = player.Gold;
        await RunEvent(driver, run, merchant, _ =>
        {
            if (bought is null)
            {
                // Avoid pickup HP effects changing the defeat fixture.
                bought = merchant.Inventory.Relics.First(e => e.Relic is not (FakeLeesWaffle or FakeMango));
                return new CustomEventDecision.BuyRelic(bought);
            }
            return new CustomEventDecision.UsePotion(potion);
        });
        Assert.True(merchant.IsFinished);
        Assert.False(merchant.HasPendingForcedCombat);
        Assert.False(merchant.IsAwaitingForcedCombat);
        Assert.Equal(victory, !player.Creature.IsDead);
        Assert.Equal(originalGold - bought!.Price + (victory ? 300 : 0), player.Gold);
        Assert.Equal(originalDeck + (victory ? 1 : 0), player.Deck.Cards.Count);
        Assert.Equal(victory ? 7 : 1, player.Relics.Count(r => r.GetType().Name.StartsWith("Fake")));
        Assert.Equal(victory, player.Relics.Any(r => r is FakeMerchantsRug));
        Assert.Single(player.Relics, r => r.Id == bought.Relic.Id);
        Assert.DoesNotContain(player.PotionSlots, p => ReferenceEquals(p, potion));
        Assert.Null(player.PlayerCombatState);
        Assert.True(player.CanUseOrRemovePotions);
        Assert.Null(run.CurrentRoom);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BattlewornDummy_DoesNotGainNormalCombatGoldOrCards(bool driver)
    {
        var run = NewRun();
        Player player = run.Players[0];
        int gold = player.Gold;
        int cards = player.Deck.Cards.Count;
        var dummy = (BattlewornDummy)ModelDb.Event<BattlewornDummy>().MutableClone();
        await RunEvent(driver, run, dummy);
        Assert.True(dummy.IsFinished);
        Assert.Equal(gold, player.Gold);
        Assert.Equal(cards, player.Deck.Cards.Count);
        Assert.Null(player.PlayerCombatState);
        Assert.Null(run.CurrentRoom);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PotionLock_IsReleasedWhenEventOptionThrows(bool driver)
    {
        var run = NewRun();
        Player player = run.Players[0];
        player.AddPotionInternal(ModelDb.Potion<FoulPotion>());
        var stone = (StoneOfAllTime)ModelDb.Event<StoneOfAllTime>().MutableClone();
        // Simulate a stale captured potion: the option must fail and still release its lock.
        var room = new EventRoom(() => stone);
        run.PushRoom(room);
        await room.Enter(run);
        Assert.False(player.CanUseOrRemovePotions);
        var combat = new CombatRoom(() => (MonsterModel)ModelDb.Monster<FakeMerchantMonster>().MutableClone());
        run.PushRoom(combat);
        await combat.Enter(run);
        try { Assert.False(await CombatPotionPolicy.TryUseFirstAvailableAsync(combat.Engine, player)); }
        finally { await combat.Exit(run); run.PopCurrentRoom(); }
        Assert.IsType<FoulPotion>(player.PotionSlots[0]);
        player.RemovePotionInternal(player.PotionSlots[0]!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver
            ? new RunDriver(run, new Decisions()).DriveEventAsync(room)
            : new RunEngine(run, PickFirst).DriveEventAsync(room));
        Assert.True(player.CanUseOrRemovePotions);
        await room.Exit(run);
        run.PopCurrentRoom();
    }

    private static RunState NewRun()
    {
        var run = new RunState("shared-integration", new ActDefinition[] { new Overgrowth(), new Hive(), new Glory() });
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        player.Gold = 500;
        player.Creature.SetMaxHpInternal(10000);
        player.Creature.HealInternal(10000);
        return run;
    }

    private static MapPoint PickFirst(IEnumerable<MapPoint> options) => options.OrderBy(p => p.coord.col).First();

    private static async Task RunEvent(bool driver, RunState run, EventModel @event,
        Func<EventModel, CustomEventDecision>? custom = null)
    {
        run.Map.StartingMapPoint.PointType = MapPointType.Monster;
        PickFirst(run.Map.StartingMapPoint.Children).PointType = MapPointType.Ancient;
        Func<RunState, AbstractRoom> create = _ => new EventRoom(() => @event);
        if (driver)
            await new RunDriver(run, new Decisions(custom), null, null, false, create).RunAsync(1);
        else
            await new RunEngine(run, PickFirst, null, null, false, create,
                custom is null ? null : e => Task.FromResult(custom(e))).RunAsync(1);
    }

    private sealed class Decisions(Func<EventModel, CustomEventDecision>? custom = null) : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => Task.FromResult(PickFirst(options));
        public Task<CustomEventDecision> ChooseCustomEventActionAsync(EventModel @event) =>
            Task.FromResult(custom?.Invoke(@event) ?? CustomEventDecisionPolicy.ChooseDefault(@event));
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
        {
            var player = state.Players[0];
            var card = player.PlayerCombatState!.Hand.Cards.FirstOrDefault(c => c.CanPlay(out _));
            return Task.FromResult<CombatDecision>(card is null ? new CombatDecision.EndTurn()
                : new CombatDecision.PlayCard(card, card.TargetType == TargetType.AnyEnemy ? state.HittableEnemies[0] : null));
        }
    }
}
