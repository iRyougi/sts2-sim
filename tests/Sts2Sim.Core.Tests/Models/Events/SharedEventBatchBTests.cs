using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Events;

public sealed class SharedEventBatchBTests : IDisposable
{
    public SharedEventBatchBTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task SlipperyBridge_HoldsAvoidConcreteCardClassAndIncreaseDamageBeforeRemoval()
    {
        var (run, player) = CreateRun();
        foreach (var card in player.Deck.Cards.ToArray()) CardPileCmd.Remove(card);
        await AddCard<DaggerThrow>(player);
        await AddCard<DaggerThrow>(player);
        await AddCard<DaggerSpray>(player);
        var ev = Begin<SlipperyBridge>(run, player);
        CardModel first = ev.RandomCardToLose;
        int hp = player.Creature.CurrentHp;
        Assert.Equal(1, ev.Rng.Counter);
        await Choose(ev, "HOLD_ON_0");
        Assert.NotEqual(first.GetType(), ev.RandomCardToLose.GetType());
        Assert.Equal(hp - 3, player.Creature.CurrentHp);
        await Choose(ev, "HOLD_ON_1");
        Assert.Equal(hp - 7, player.Creature.CurrentHp);
        Assert.Equal(3, ev.Rng.Counter);
        CardModel removed = ev.RandomCardToLose;
        await Choose(ev, "OVERCOME");
        Assert.DoesNotContain(player.Deck.Cards, c => ReferenceEquals(c, removed));
    }

    [Fact]
    public async Task StoneOfAllTime_PushEnchantsAttackAndLiftConsumesPromisedPotion()
    {
        var (run, player) = CreateRun(1);
        var potion = player.AddPotionInternal(ModelDb.Potion<FruitJuice>());
        var ev = Begin<StoneOfAllTime>(run, player);
        int hp = player.Creature.CurrentHp;
        Assert.False(player.CanUseOrRemovePotions);
        await Choose(ev, "PUSH");
        var enchantment = Assert.Single(player.Deck.Cards.SelectMany(c => c.Enchantments).OfType<Vigorous>());
        Assert.Equal(hp - 6, player.Creature.CurrentHp);
        Assert.Equal(8m, enchantment.EnchantDamageAdditive(6, ValueProp.Move));
        await enchantment.AfterCardPlayed(new CardPlay
        {
            Card = enchantment.Owner, Player = player, Target = null,
            ResultPile = PileType.Discard, Resources = new ResourceInfo(), IsAutoPlay = false,
            PlayIndex = 0, PlayCount = 1,
        });
        Assert.Equal(0m, enchantment.EnchantDamageAdditive(6, ValueProp.Move));
        Assert.True(player.CanUseOrRemovePotions);
        int maxHp = player.Creature.MaxHp;
        await Choose(Begin<StoneOfAllTime>(run, player), "LIFT");
        Assert.Equal(maxHp + 10, player.Creature.MaxHp);
        Assert.DoesNotContain(potion, player.PotionSlots);
    }

    [Theory]
    [InlineData(typeof(StrikeSilent), typeof(Silent), CardRarity.None)]
    [InlineData(typeof(StrikeRegent), typeof(Regent), CardRarity.None)]
    [InlineData(typeof(Clumsy), null, CardRarity.Curse)]
    [InlineData(typeof(Dazed), null, CardRarity.Status)]
    [InlineData(typeof(LanternKey), null, CardRarity.None)]
    public async Task Symbiote_ApproachCorruptsAttackAndFirePreservesTheSelectedCardsPool(
        Type originalType, Type? expectedCharacter, CardRarity expectedRarity)
    {
        var (run, player) = CreateRun(1);
        var ev = Begin<Symbiote>(run, player);
        await Choose(ev, "APPROACH");
        var enchantment = Assert.Single(player.Deck.Cards.SelectMany(c => c.Enchantments).OfType<Corrupted>());
        Assert.Equal(1.5m, enchantment.EnchantDamageMultiplicative(6, ValueProp.Move));
        int hp = player.Creature.CurrentHp;
        await enchantment.OnPlay(enchantment.Owner);
        Assert.Equal(hp - 2, player.Creature.CurrentHp);
        foreach (var card in player.Deck.Cards.ToArray()) CardPileCmd.Remove(card);
        var first = (CardModel)ModelDb.Get(originalType).MutableClone();
        first.AssignOwner(player);
        await CardPileCmd.AddToDeck(first);
        await Choose(Begin<Symbiote>(run, player), "KILL_WITH_FIRE");
        var replacement = Assert.Single(player.Deck.Cards);
        Assert.DoesNotContain(player.Deck.Cards, c => ReferenceEquals(c, first));
        Assert.NotEqual(first.Id, replacement.Id);
        if (expectedCharacter is not null)
            Assert.Contains(((CharacterModel)ModelDb.Get(expectedCharacter)).CardPool.AllCards, c => c.Id == replacement.Id);
        else if (expectedRarity != CardRarity.None)
            Assert.Equal(expectedRarity, replacement.Rarity);
        else Assert.True(replacement.IsColorless);
    }

    [Fact]
    public async Task TeaMaster_ChargesTheChosenTeaPrice()
    {
        var (run, player) = CreateRun();
        player.Gold = 150;
        await Choose(Begin<TeaMaster>(run, player), "EMBER_TEA");
        Assert.Equal(0, player.Gold);
        Assert.Contains(player.Relics, r => r is EmberTea);
        var locked = Begin<TeaMaster>(run, player);
        Assert.Equal(new[] { "BONE_TEA_LOCKED", "EMBER_TEA_LOCKED", "TEA_OF_DISCOURTESY" }, locked.CurrentOptions.Select(o => o.Key));
        await Choose(locked, "TEA_OF_DISCOURTESY");
        Assert.Contains(player.Relics, r => r is TeaOfDiscourtesy);
    }

    [Fact]
    public async Task FutureOfPotions_MapsHiddenFourthPotionAndOffersThreeUpgradedMatchingCards()
    {
        var (run, player) = CreateRun();
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), player);
        player.GrowPotionSlots(2);
        for (int i = 0; i < 4; i++) player.AddPotionInternal(ModelDb.Potion<BlockPotion>());
        var ev = Begin<TheFutureOfPotions>(run, player);
        Assert.Equal(3, ev.CurrentOptions.Count);
        Assert.Equal(4, ev.Rng.Counter);
        Assert.False(player.CanUseOrRemovePotions);
        int before = player.PlayerRng.Rewards.Counter;
        await ev.ChooseOption(ev.CurrentOptions[0]);
        Assert.True(ev.TryDequeuePendingRewardOffer(out var rewards));
        Assert.Equal(3, rewards!.Card.Options.Count);
        Assert.All(rewards.Card.Options, card =>
        {
            Assert.True(card.IsUpgraded);
            Assert.Equal(CardRarity.Common, card.Rarity);
            Assert.Equal(rewards.Card.Options[0].Type, card.Type);
        });
        Assert.Equal(3, player.PlayerRng.Rewards.Counter - before);
        Assert.Equal(1, Assert.Single(player.Relics.OfType<SilverCrucible>()).TimesUsed);
        Assert.Equal(3, player.PotionSlots.OfType<PotionModel>().Count());
        Assert.True(ev.IsFinished);
        Assert.True(player.CanUseOrRemovePotions);
        await rewards.Card.SelectOption(rewards.Card.Options[0]);
        Assert.True(player.CanUseOrRemovePotions);
    }

    [Fact]
    public async Task LegendsWereTrue_MapRewardWaitsForActTwoQuestTreasureAndPreservesNormalReward()
    {
        var (run, player) = CreateRun();
        int gold = player.Gold;
        await Choose(Begin<TheLegendsWereTrue>(run, player), "NAB_THE_MAP");
        var mapCard = Assert.Single(player.Deck.Cards.OfType<SpoilsMap>());
        Assert.Equal(gold, player.Gold);
        run.AdvanceToNextAct();
        Assert.IsType<SpoilsActMap>(run.Map);
        var treasure = Assert.Single(run.Map.GetAllMapPoints(), p => p.PointType == MapPointType.Treasure);
        Assert.Contains(mapCard, treasure.Quests);
        Assert.Single(run.Map.GetPointsInRow(treasure.coord.row));
        var extra = (SpoilsMap)await AddCard<SpoilsMap>(player);
        extra.ModifyGeneratedMapLate(run, run.Map, 1);
        await extra.AfterMapGenerated(run.Map, 1);
        await CardPileCmd.RemoveFromDeck(player, extra);
        Assert.DoesNotContain(treasure.Quests, q => ReferenceEquals(q, extra));
        extra = (SpoilsMap)await AddCard<SpoilsMap>(player);
        extra.ModifyGeneratedMapLate(run, run.Map, 1);
        await extra.AfterMapGenerated(run.Map, 1);
        await CardCmd.TransformToRandom(extra, new Sts2Sim.Core.Random.Rng(19), run);
        Assert.DoesNotContain(treasure.Quests, q => ReferenceEquals(q, extra));
        extra = (SpoilsMap)await AddCard<SpoilsMap>(player);
        extra.ModifyGeneratedMapLate(run, run.Map, 1);
        await extra.AfterMapGenerated(run.Map, 1);
        player.Gold = 999;
        var merchant = new MerchantRoom();
        await merchant.Enter(run);
        int price = merchant.Inventory.CardRemoval.Price;
        Assert.Equal(price, await merchant.BuyCardRemovalWithPriceAsync(extra, player));
        Assert.Equal(999 - price, player.Gold);
        Assert.DoesNotContain(treasure.Quests, q => ReferenceEquals(q, extra));
        Assert.DoesNotContain(player.Deck.Cards, c => ReferenceEquals(c, extra));
        await merchant.Exit(run);
        gold = player.Gold;
        run.AddVisitedMapCoord(treasure.coord);
        int relics = player.Relics.Count;
        await new TreasureRoom(47).Enter(run);
        Assert.Equal(gold + 647, player.Gold);
        Assert.Equal(relics + 1, player.Relics.Count);
        Assert.DoesNotContain(mapCard, player.Deck.Cards);
        Assert.Empty(treasure.Quests);
        Assert.Contains(mapCard.Id, run.CompletedQuests);
    }

    [Fact]
    public async Task ThisOrThat_RollsGoldBeforeEitherChoiceAndOrnateAddsCurseAndRelic()
    {
        var (run, player) = CreateRun();
        var ev = Begin<ThisOrThat>(run, player);
        Assert.Equal(1, ev.Rng.Counter);
        int hp = player.Creature.CurrentHp;
        int gold = player.Gold;
        await Choose(ev, "PLAIN");
        Assert.Equal(hp - 6, player.Creature.CurrentHp);
        Assert.InRange(player.Gold - gold, 41, 68);
        int relics = player.Relics.Count;
        await Choose(Begin<ThisOrThat>(run, player), "ORNATE");
        Assert.Contains(player.Deck.Cards, c => c is Clumsy);
        Assert.Equal(relics + 1, player.Relics.Count);
    }

    [Fact]
    public async Task Repy_TwoKeysUnlockBothRewardsAndRecordQuestCompletion()
    {
        var (run, player) = CreateRun(2);
        await AddCard<LanternKey>(player);
        await AddCard<LanternKey>(player);
        var ev = Begin<WarHistorianRepy>(run, player);
        Assert.False(ev.IsAllowed(run));
        await Choose(ev, "UNLOCK_CAGE");
        Assert.True(run.FreedRepy);
        Assert.Contains(player.Relics, r => r is HistoryCourse);
        Assert.Equal("UNLOCK_CHEST", Assert.Single(ev.CurrentOptions).Key);
        var probe = player.PlayerRng.Rewards.CloneExact();
        ModelId[] expectedPotions = Enumerable.Range(0, 2)
            .Select(_ => PotionFactory.CreateRandomOutOfCombat(player, probe)!.Id).ToArray();
        int rewardsCounter = player.PlayerRng.Rewards.Counter;
        await Choose(ev, "UNLOCK_CHEST");
        Assert.True(ev.IsFinished);
        Assert.Empty(player.Deck.Cards.OfType<LanternKey>());
        Assert.Equal(2, run.CompletedQuests.Count);
        Assert.True(ev.TryDequeuePendingRewardOffer(out var rewards));
        Assert.Equal(2, rewards!.ExtraRewards.OfType<PotionReward>().Count());
        Assert.Equal(2, rewards.ExtraRewards.OfType<RelicReward>().Count());
        Assert.Equal(expectedPotions, rewards.ExtraRewards.OfType<PotionReward>().Select(r => r.Potion!.Id));
        Assert.All(rewards.ExtraRewards.OfType<PotionReward>(), reward =>
            Assert.Contains(PotionFactory.GetOutOfCombatPool(player), potion => potion.Id == reward.Potion!.Id));
        Assert.Equal(6, player.PlayerRng.Rewards.Counter - rewardsCounter);
    }

    [Fact]
    public async Task Wongo_LeaveDowngradesOneCardAndOnlyDrawsWhenACandidateExists()
    {
        var (run, player) = CreateRun(1);
        var ev = Begin<WelcomeToWongos>(run, player);
        await Choose(ev, "LEAVE");
        Assert.Equal(0, ev.Rng.Counter);
        player.Deck.Cards[0].Upgrade();
        player.Deck.Cards[1].Upgrade();
        ev = Begin<WelcomeToWongos>(run, player);
        await Choose(ev, "LEAVE");
        Assert.Single(player.Deck.Cards, c => c.IsUpgraded);
        Assert.Equal(1, ev.Rng.Counter);
    }

    [Fact]
    public async Task Wongo_ThresholdAwardsBadgeAndExportsProgressWithoutMutatingInput()
    {
        var progress = new HeadlessProgress(1980);
        var (run, player) = CreateRun(1, progress);
        player.Gold = 300;
        await Choose(Begin<WelcomeToWongos>(run, player), "BARGAIN_BIN");
        Assert.Equal(200, player.Gold);
        Assert.Contains(player.Relics, r => r is WongoCustomerAppreciationBadge);
        Assert.Equal(1980, progress.WongoPoints);
        Assert.Equal(32, run.WongoPointsEarned);
        Assert.Equal(2012, run.ExportProgress().WongoPoints);
        var rng = new Sts2Sim.Core.Random.Rng(17);
        var bag = new RelicGrabBag(rng, [ModelDb.Relic<AmethystAubergine>(), ModelDb.Relic<Anchor>()]);
        int counter = rng.Counter;
        Assert.IsType<Anchor>(bag.PullFromFront(RelicRarity.Common, run, r => r.IsAllowedInShops));
        Assert.IsType<AmethystAubergine>(bag.PullFromFront(RelicRarity.Common, run));
        Assert.Equal(counter, rng.Counter);
    }

    [Theory]
    [InlineData(typeof(StoneOfAllTime), 0, false)]
    [InlineData(typeof(StoneOfAllTime), 1, true)]
    [InlineData(typeof(Symbiote), 0, false)]
    [InlineData(typeof(Symbiote), 1, true)]
    [InlineData(typeof(TeaMaster), 2, false)]
    [InlineData(typeof(TheLegendsWereTrue), 0, true)]
    [InlineData(typeof(TheLegendsWereTrue), 1, false)]
    [InlineData(typeof(TheFutureOfPotions), 0, true)]
    [InlineData(typeof(WelcomeToWongos), 1, true)]
    [InlineData(typeof(WelcomeToWongos), 2, false)]
    [InlineData(typeof(WarHistorianRepy), 2, false)]
    public void GatesRespectActAndInventory(Type eventType, int actIndex, bool allowed)
    {
        var (run, player) = CreateRun(actIndex);
        player.Gold = 150;
        player.AddPotionInternal(ModelDb.Potion<BlockPotion>());
        player.AddPotionInternal(ModelDb.Potion<BlockPotion>());
        Assert.Equal(allowed, ((EventModel)ModelDb.Get(eventType)).IsAllowed(run));
    }

    private static (RunState Run, Player Player) CreateRun(int actIndex = 0, HeadlessProgress? progress = null)
    {
        var run = new RunState("shared-b", [new Overgrowth(), new Hive(), new Glory()], progress: progress);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        run.ConfigureCardSelectionSource(new FirstCardSelection());
        while (run.CurrentActIndex < actIndex) run.AdvanceToNextAct();
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

    private sealed class FirstCardSelection : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
            Task.FromResult<IReadOnlyList<CardModel>>(request.Candidates.Take(request.MinCount).ToArray());
    }

    private static async Task<CardModel> AddCard<T>(Player player) where T : CardModel
    {
        var card = (CardModel)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        await CardPileCmd.AddToDeck(card);
        return card;
    }
}
