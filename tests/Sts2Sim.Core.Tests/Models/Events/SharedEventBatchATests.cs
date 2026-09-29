using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

public sealed class SharedEventBatchATests : IDisposable
{
    public SharedEventBatchATests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task BrainLeechKnowledgeOffersFiveAndAddsOneWithoutUpgradeRolls()
    {
        var (run, player) = CreateRun();
        await RelicCmd.Obtain(ModelDb.Relic<Glitter>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<SilkenTress>(), player);
        var silver = Assert.Single(player.Relics.OfType<SilverCrucible>());
        var silken = Assert.Single(player.Relics.OfType<SilkenTress>());
        var ev = Begin<BrainLeech>(run, player);
        int cards = player.Deck.Cards.Count, rng = player.PlayerRng.Rewards.Counter;
        await Choose(ev, "SHARE_KNOWLEDGE");
        Assert.Equal(cards + 1, player.Deck.Cards.Count);
        Assert.Equal(10, player.PlayerRng.Rewards.Counter - rng);
        Assert.True(ev.IsFinished);
        Assert.IsType<Glam>(Assert.Single(player.Deck.Cards.Last().Enchantments));
        Assert.False(player.Deck.Cards.Last().IsUpgraded);
        Assert.Equal(0, silver.TimesUsed);
        Assert.False(silken.IsUsedUp);

        var rip = Begin<BrainLeech>(run, player);
        await Choose(rip, "RIP");
        Assert.True(rip.TryDequeuePendingRewardOffer(out var rewards));
        var reward = rewards!.Card;
        Assert.Equal(1, silver.TimesUsed);
        Assert.True(silken.IsUsedUp);
        Assert.All(reward.Options, c => Assert.IsType<Glam>(Assert.Single(c.Enchantments)));
        reward.Populate(run);
        Assert.Equal(1, silver.TimesUsed);
    }

    [Fact]
    public async Task BrainLeechFatalRipDoesNotGenerateAnUnclaimableReward()
    {
        var (run, player) = CreateRun();
        await CreatureCmd.Damage(run, player.Creature, player.Creature.CurrentHp - 3,
            ValueProp.Unblockable | ValueProp.Unpowered);
        var ev = Begin<BrainLeech>(run, player);
        int before = player.PlayerRng.Rewards.Counter;
        await Choose(ev, "RIP");
        Assert.True(player.Creature.IsDead);
        Assert.False(ev.HasPendingRewardOffers);
        Assert.Equal(before, player.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public async Task DollRoomExamineCostsFifteenAndOffersAllThreeDolls()
    {
        var (run, player) = CreateRun();
        var ev = Begin<DollRoom>(run, player);
        int hp = player.Creature.CurrentHp;
        await Choose(ev, "EXAMINE");
        Assert.Equal(hp - 15, player.Creature.CurrentHp);
        Assert.Equal(3, ev.CurrentOptions.Count);
        Assert.Equal(2, ev.Rng.Counter);
        await ev.ChooseOption(ev.CurrentOptions[0]);
        Assert.Contains(player.Relics, r => r is DaughterOfTheWind or BingBong or MrStruggles);
    }

    [Fact]
    public async Task PotionCourierOffersThreeSeparateFoulPotions()
    {
        var (run, player) = CreateRun();
        var ev = Begin<PotionCourier>(run, player);
        await Choose(ev, "GRAB_POTIONS");
        Assert.True(ev.TryDequeuePendingRewardOffer(out var rewards));
        var potions = rewards!.ExtraRewards.Cast<PotionReward>().ToArray();
        Assert.Equal(3, potions.Length);
        Assert.All(potions, p => Assert.IsType<FoulPotion>(p.Potion));
        Assert.Equal(3, potions.Select(p => p.Potion).Distinct().Count());
    }

    [Fact]
    public async Task RanwidLocksThePromisedPotionAndTradesItForOneRelic()
    {
        var (run, player) = CreateRun();
        var potion = player.AddPotionInternal(ModelDb.Potion<FruitJuice>());
        player.AddRelicInternal(ModelDb.Relic<Anchor>());
        var ev = Begin<RanwidTheElder>(run, player);
        int relics = player.Relics.Count;
        Assert.False(player.CanUseOrRemovePotions);
        Assert.Equal(2, ev.Rng.Counter);
        await Choose(ev, "POTION");
        Assert.DoesNotContain(potion, player.PotionSlots);
        Assert.Equal(relics + 1, player.Relics.Count);
        Assert.True(player.CanUseOrRemovePotions);
    }

    [Fact]
    public async Task RelicTraderReservesThreeReplacementsAndTradesOnlyTheChosenPair()
    {
        var (run, player) = CreateRun();
        foreach (var relic in ModelDb.All<RelicModel>().Where(r => r.IsTradable).Take(5))
            player.AddRelicInternal(relic);
        var owned = player.Relics.ToArray();
        int rng = player.PlayerRng.Rewards.Counter;
        var ev = Begin<RelicTrader>(run, player);
        Assert.Equal(4, ev.Rng.Counter);
        Assert.Equal(3, player.PlayerRng.Rewards.Counter - rng);
        Assert.Equal(new[] { "TOP", "MIDDLE", "BOTTOM" }, ev.CurrentOptions.Select(o => o.Key));
        await Choose(ev, "MIDDLE");
        Assert.Equal(1, owned.Count(r => !player.Relics.Contains(r)));
    }

    [Fact]
    public async Task CheeseGorgeAddsTwoCommonCardsFromEightUniformChoices()
    {
        var (run, player) = CreateRun();
        await RelicCmd.Obtain(ModelDb.Relic<Glitter>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<SilkenTress>(), player);
        var ev = Begin<RoomFullOfCheese>(run, player);
        var old = player.Deck.Cards.ToArray();
        int rng = player.PlayerRng.Rewards.Counter;
        await Choose(ev, "GORGE");
        var added = player.Deck.Cards.Except(old).ToArray();
        Assert.Equal(2, added.Length);
        Assert.All(added, c => Assert.Equal(CardRarity.Common, c.Rarity));
        Assert.All(added, c => Assert.IsType<Glam>(Assert.Single(c.Enchantments)));
        Assert.All(added, c => Assert.False(c.IsUpgraded));
        Assert.Equal(0, Assert.Single(player.Relics.OfType<SilverCrucible>()).TimesUsed);
        Assert.False(Assert.Single(player.Relics.OfType<SilkenTress>()).IsUsedUp);
        Assert.Equal(8, player.PlayerRng.Rewards.Counter - rng);
    }

    [Fact]
    public async Task SelfHelpBookEnchantsBlockSkillAndLocksMissingPowerChoice()
    {
        var (run, player) = CreateRun();
        foreach (var attack in player.Deck.Cards.Where(c => c.Type == CardType.Attack).ToArray())
            CardPileCmd.Remove(attack);
        Assert.Contains(player.Deck.Cards, c => c is DefendSilent);
        Assert.Contains(player.Deck.Cards, c => c is Survivor);
        var ev = Begin<SelfHelpBook>(run, player);
        Assert.True(ev.CurrentOptions[0].IsLocked);
        Assert.True(ev.CurrentOptions.Single(o => o.Key == "READ_ENTIRE_BOOK_LOCKED").IsLocked);
        var options = ev.CurrentOptions;
        ev = (SelfHelpBook)ModelDb.Event<SelfHelpBook>().MutableClone();
        run.Map.StartingMapPoint.PointType = MapPointType.Monster;
        run.Map.StartingMapPoint.Children.OrderBy(p => p.coord.col).First().PointType = MapPointType.Ancient;
        await new RunEngine(run, points => points.OrderBy(p => p.coord.col).First(),
            null, null, false, _ => new EventRoom(() => ev)).RunAsync(1);
        Assert.True(ev.IsFinished);
        Assert.Null(run.CurrentRoom);
        var enchanted = player.Deck.Cards.Single(c => c.Enchantments.Any());
        Assert.True(enchanted.GainsBlock);
        Assert.Equal(2m, Assert.IsType<Nimble>(Assert.Single(enchanted.Enchantments)).Magnitude);
        Assert.False(ModelDb.GetById<Nimble>(ModelDb.GetId<Nimble>()).CanEnchant(ModelDb.Card<StrikeSilent>()));

        IRunDecisionSource decisions = new Sts2Sim.Core.Tests.Runs.FirstChoiceDecisionSource();
        Assert.Equal("READ_PASSAGE", (await decisions.ChooseEventOptionAsync(options)).Key);
        var excluded = new EventOption("Kaleidoscope", () => Task.CompletedTask);
        Assert.Equal("READ_PASSAGE", (await decisions.ChooseEventOptionAsync([excluded, .. options])).Key);
        Assert.Same(excluded, await decisions.ChooseEventOptionAsync([options[0], excluded]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => decisions.ChooseEventOptionAsync([options[0]]));
    }

    [Fact]
    public async Task FakeMerchantHasSixPricedSlotsAndFoulPotionRewardsOnlyUnboughtStock()
    {
        var (run, player) = CreateRun();
        player.Gold = 1000;
        int shops = player.PlayerRng.Shops.Counter;
        var room = new EventRoom(() => (EventModel)ModelDb.Event<FakeMerchant>().MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        var ev = Assert.IsType<FakeMerchant>(room.Event);
        Assert.Empty(ev.CurrentOptions);
        Assert.False(ev.IsFinished);
        Assert.Equal(8, ev.Rng.Counter);
        Assert.Equal(6, player.PlayerRng.Shops.Counter - shops);
        Assert.Equal(6, ev.Inventory.Relics.Count);
        Assert.All(ev.Inventory.Relics, e => Assert.InRange(e.Price, 43, 57));
        int[] prices = ev.Inventory.Relics.Select(e => e.Price).ToArray();
        await RelicCmd.Obtain(ModelDb.Relic<MembershipCard>(), player);
        Assert.Equal(prices, ev.Inventory.Relics.Select(e => e.Price));
        await ev.Buy(ev.Inventory.Relics[0]);
        Assert.Equal(1000 - prices[0], player.Gold);
        Assert.Equal(6, player.PlayerRng.Shops.Counter - shops);
        var potion = player.AddPotionInternal(ModelDb.Potion<FoulPotion>());
        await PotionCmd.Use(potion, player, null);
        Assert.True(ev.IsAwaitingForcedCombat);
        Assert.Equal(300, ev.ForcedCombatGold);
        Assert.Equal(6, ev.ForcedCombatExtraRewards.Count);
        Assert.DoesNotContain(ev.ForcedCombatExtraRewards.Cast<RelicReward>(),
            r => r.Relic!.Id == ev.Inventory.Relics[0].Relic.Id);
    }

    [Fact]
    public async Task CrystalSpherePaymentPreservesGridAndSixDivinations()
    {
        var (run, player) = CreateRun();
        var ev = Begin<CrystalSphere>(run, player);
        await Choose(ev, "PAYMENT_PLAN");
        Assert.Contains(player.Deck.Cards, c => c is Debt);
        Assert.NotNull(ev.Game);
        Assert.Equal(6, ev.Game.DivinationCount);
        Assert.Equal(11, ev.Game.Width);
        Assert.InRange(ev.Rng.Counter, 2, 151);
        for (int i = 0; i < 6; i++) await ev.RevealAsync(5, 5, big: true);
        Assert.True(ev.IsFinished);
        Assert.Equal(0, ev.Game.DivinationCount);
    }

    [Theory]
    [InlineData(0, true, false, false, false)]
    [InlineData(1, true, true, true, true)]
    [InlineData(2, false, true, false, true)]
    public void GatesUseActAndOwnerResources(int act, bool brain, bool sphere, bool doll, bool courier)
    {
        var (run, player) = CreateRun();
        for (int i = 0; i < act; i++) run.AdvanceToNextAct();
        player.Gold = 100;
        Assert.Equal(brain, ModelDb.Event<BrainLeech>().IsAllowed(run));
        Assert.Equal(sphere, ModelDb.Event<CrystalSphere>().IsAllowed(run));
        Assert.Equal(doll, ModelDb.Event<DollRoom>().IsAllowed(run));
        Assert.Equal(courier, ModelDb.Event<PotionCourier>().IsAllowed(run));
        Assert.Equal(brain, ModelDb.Event<RoomFullOfCheese>().IsAllowed(run));
        Assert.True(ModelDb.Event<SelfHelpBook>().IsAllowed(run));
        Assert.False(ModelDb.Event<RanwidTheElder>().IsAllowed(run));
        Assert.False(ModelDb.Event<RelicTrader>().IsAllowed(run));
        Assert.Equal(act > 0, ModelDb.Event<FakeMerchant>().IsAllowed(run));
    }

    [Fact]
    public async Task CrystalSphereRevealedCardRewardConsumesSixEventRollsIncludingUpgrades()
    {
        var (run, player) = CreateRun();
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), player);
        var silver = Assert.Single(player.Relics.OfType<SilverCrucible>());
        run.AdvanceToNextAct();
        var rng = new Rng(24);
        var game = new CrystalSphereMinigame(player, rng, 1);
        Assert.True(game.PlacedAllItems);
        Assert.Equal(15, rng.Counter);
        var item = game.Items.Single(i => i.Kind == CrystalSphereItemKind.CardReward && i.CardRarity == CardRarity.Common);
        var position = item.Position!.Value;
        int before = rng.Counter, rewardsBefore = player.PlayerRng.Rewards.Counter;
        await game.RevealAsync(position.X, position.Y);
        Assert.Contains(item, game.RevealedItems);
        var cardReward = Assert.Single(game.Rewards.OfType<CardReward>());
        Assert.Equal(1, silver.TimesUsed);
        cardReward.Populate(run);
        Assert.Equal(1, silver.TimesUsed);
        Assert.Equal(3, cardReward.Options.Count);
        Assert.All(cardReward.Options, c => Assert.Equal(CardRarity.Common, c.Rarity));
        Assert.Equal(6 + game.RevealedItems.Count(i => i.Kind is CrystalSphereItemKind.Potion or CrystalSphereItemKind.Relic),
            rng.Counter - before);
        Assert.Equal(rewardsBefore, player.PlayerRng.Rewards.Counter);
        Assert.Contains(cardReward.Options, c => c.IsUpgraded);
    }

    [Fact]
    public async Task FakeMerchantThrowFollowUpExcludesEnrageAndSpewDealsEightHits()
    {
        var (run, player) = CreateRun();
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<FakeMerchantMonster>().MutableClone());
        await room.Enter(run);
        var monster = (FakeMerchantMonster)room.Engine.State.HittableEnemies.Single().Monster!;
        Assert.Equal(165, monster.Creature.MaxHp);
        Assert.Equal("SWIPE_MOVE", monster.NextMove!.Id);
        var machine = monster.MoveStateMachine!;
        var normal = (RandomBranchState)machine.States["RAND_MOVE"];
        var attacks = (RandomBranchState)machine.States["RAND_ATTACK_MOVE"];
        var enrage = normal.States.Single(s => s.StateId == "ENRAGE_MOVE");
        Assert.Equal(1f, enrage.GetWeight());
        Assert.Equal(3, enrage.Cooldown);
        Assert.Equal(MoveRepeatType.CannotRepeat, enrage.RepeatType);
        Assert.Equal(0, enrage.MaxTimes);
        Assert.DoesNotContain(attacks.States, s => s.StateId == "ENRAGE_MOVE");
        monster.SetMoveImmediate((MoveState)machine.States["THROW_RELIC_MOVE"], true);
        await monster.PerformMove();
        monster.RollMove(room.Engine.State.Allies);
        Assert.Contains(monster.NextMove!.Id, new[] { "SWIPE_MOVE", "SPEW_COINS_MOVE" });
        int hp = player.Creature.CurrentHp;
        monster.SetMoveImmediate((MoveState)machine.States["SPEW_COINS_MOVE"], true);
        await monster.PerformMove();
        Assert.Equal(hp - 16, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task CrystalSphereConstructsAllPotionRewardsBeforePopulatingCardRewards()
    {
        var (run, player) = CreateRun();
        var rng = new Rng(24);
        var game = new CrystalSphereMinigame(player, rng, 2);
        var probe = rng.CloneExact();
        var card = game.Items.First(i => i.Kind == CrystalSphereItemKind.CardReward);
        var potion = game.Items.First(i => i.Kind == CrystalSphereItemKind.Potion);
        await game.RevealAsync(card.Position!.Value.X, card.Position.Value.Y);
        await game.RevealAsync(potion.Position!.Value.X, potion.Position.Value.Y + 1);
        var expected = game.RevealedItems.Where(i => i.Kind == CrystalSphereItemKind.Potion)
            .Select(i => probe.NextItem(PotionFactory.GetOutOfCombatPool(player).Where(p => p.Rarity == i.PotionRarity))!.Id)
            .ToArray();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, game.Rewards.OfType<PotionReward>().Select(r => r.Potion!.Id));
    }

    [Fact]
    public async Task FoulPotionTargetsActiveMerchantAndDamagesPlayerInCombat()
    {
        var (run, player) = CreateRun();
        var merchant = new MerchantRoom();
        run.PushRoom(merchant);
        await merchant.Enter(run);
        await RelicCmd.Obtain(ModelDb.Relic<BingBong>(), player);
        player.Gold = 1000;
        int deckBefore = player.Deck.Cards.Count;
        await merchant.Buy(merchant.Inventory.Cards[0], player);
        Assert.Equal(deckBefore + 2, player.Deck.Cards.Count);
        var potion = player.AddPotionInternal(ModelDb.Potion<FoulPotion>());
        merchant.IsInventoryOpen = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, player, null));
        merchant.IsInventoryOpen = false;
        int gold = player.Gold;
        await PotionCmd.Use(potion, player, null);
        Assert.Equal(gold + 100, player.Gold);
        await merchant.Exit(run);
        run.PopCurrentRoom();
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        int hp = player.Creature.CurrentHp;
        var enemy = room.Engine.State.HittableEnemies.Single();
        int enemyHp = enemy.CurrentHp;
        potion = player.AddPotionInternal(ModelDb.Potion<FoulPotion>());
        await PotionCmd.Use(potion, player, null);
        Assert.Equal(hp - 12, player.Creature.CurrentHp);
        Assert.Equal(enemyHp - 12, enemy.CurrentHp);
    }

    [Fact]
    public async Task SharpAndNimbleModifyOnlyTheirAttachedCardDamageAndBlock()
    {
        var (run, player) = CreateRun();
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        var strike = (CardModel)ModelDb.Card<StrikeSilent>().MutableClone();
        strike.AssignOwner(player);
        await CardCmd.Enchant<Sharp>(strike, 2m);
        CardPileCmd.Add(strike, PileType.Hand);
        player.PlayerCombatState!.Energy = 99;
        var enemy = room.Engine.State.HittableEnemies.Single();
        int hp = enemy.CurrentHp;
        await room.Engine.PlayCardAsync(player, strike, enemy);
        Assert.Equal(hp - 8, enemy.CurrentHp);
        var defend = (CardModel)ModelDb.Card<DefendSilent>().MutableClone();
        defend.AssignOwner(player);
        await CardCmd.Enchant<Nimble>(defend, 2m);
        CardPileCmd.Add(defend, PileType.Hand);
        await room.Engine.PlayCardAsync(player, defend, null);
        Assert.Equal(7, player.Creature.Block);
    }

    private static (RunState, Player) CreateRun()
    {
        var run = new RunState("shared-batch-a", [new Overgrowth(), new Hive(), new Glory()]);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }
    private static T Begin<T>(RunState run, Player player) where T : EventModel
    {
        var ev = (T)ModelDb.Event<T>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(run);
        return ev;
    }
    private static Task Choose(EventModel ev, string key) => ev.ChooseOption(ev.CurrentOptions.Single(o => o.Key == key));
}
