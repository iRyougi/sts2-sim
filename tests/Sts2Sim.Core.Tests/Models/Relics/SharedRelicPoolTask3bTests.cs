using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;
using Xunit;

namespace Sts2Sim.Core.Tests.Models.Relics;

// Test-only decision source and early-combat observer; all relic effects use production paths.
[Collection("ModelDb")]
public sealed class SharedRelicPoolTask3bTests : RelicModel, IRunDecisionSource, IDisposable
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public MerchantRelicEntry? Purchase { get; set; }
    public RewardsSet? Offer { get; set; }
    public int RewardChoices { get; set; }
    public CardSelectionRequest? Selection { get; set; }
    public List<RestSiteDecision> RestChoices { get; } = [];
    public bool FillSlotsEarly { get; set; }
    public int EarlyCalls { get; set; }
    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(typeof(Cauldron))]
    [InlineData(typeof(DingyRug))]
    [InlineData(typeof(Kifuda))]
    [InlineData(typeof(LastingCandy))]
    [InlineData(typeof(MiniatureTent))]
    [InlineData(typeof(Orrery))]
    [InlineData(typeof(PetrifiedToad))]
    [InlineData(typeof(PunchDagger))]
    [InlineData(typeof(RoyalStamp))]
    [InlineData(typeof(WingCharm))]
    public async Task SharedRelic_ImplementsAnObservableContract(Type relicType)
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(SharedRelicPoolTask3bTests)));
        RunState run = NewRun("task3b-contract-" + relicType.Name);
        Player player = run.Players[0];
        RelicModel canonical = (RelicModel)ModelDb.Get(relicType);
        if (relicType == typeof(Cauldron) || relicType == typeof(Orrery))
            await VerifyMerchant(run, player, canonical);
        else if (relicType == typeof(Kifuda) || relicType == typeof(PunchDagger) || relicType == typeof(RoyalStamp))
        {
            run.ConfigureCardSelectionSource(this);
            CardModel[] before = player.Deck.Cards.ToArray();
            int nicheBefore = run.Rng.Niche.Counter;
            await RelicCmd.Obtain(canonical, player);
            Assert.NotNull(Selection);
            int count = relicType == typeof(Kifuda) ? 3 : 1;
            Assert.Equal(count, Selection!.MaxCount);
            Assert.Equal(relicType == typeof(Kifuda) ? 0 : 1, Selection.MinCount);
            Assert.False(Selection.Cancelable);
            CardModel[] enchanted = before.Where(card => card.Enchantments.Count != 0).ToArray();
            Assert.Equal(count, enchanted.Length);
            Assert.All(Selection.Candidates.Take(count), card => Assert.Contains(card, enchanted));
            Assert.All(enchanted, card =>
            {
                EnchantmentModel enchantment = Assert.Single(card.Enchantments);
                Assert.Equal(relicType == typeof(Kifuda) ? typeof(Adroit) : relicType == typeof(PunchDagger) ? typeof(Momentum) : typeof(RoyallyApproved), enchantment.GetType());
                Assert.Equal(relicType == typeof(Kifuda) ? 3m : relicType == typeof(PunchDagger) ? 5m : 1m, enchantment.Magnitude);
            });
            if (relicType == typeof(RoyalStamp))
            {
                Assert.True(run.Rng.Niche.Counter > nicheBefore);
                Assert.False(before.SequenceEqual(Selection.Candidates));
                Assert.All(Selection.Candidates, card => Assert.True(card.Type is CardType.Attack or CardType.Skill));
                Assert.True(enchanted[0].HasKeyword(CardKeyword.Innate));
                Assert.True(enchanted[0].HasKeyword(CardKeyword.Retain));
            }
        }
        else if (relicType == typeof(DingyRug))
        {
            await RelicCmd.Obtain(canonical, player);
            var options = new CardCreationOptions([player.Character.CardPool], CardCreationSource.Encounter,
                CardRarityOddsType.Uniform, card => card is Finesse);
            var reward = new CardReward(player, options, 1);
            reward.Populate(run);
            Assert.IsType<Finesse>(Assert.Single(reward.Options));
            await reward.SelectOption(reward.Options[0]);
            Assert.IsType<Finesse>(player.Deck.Cards.Last());
            var suppressed = new CardCreationOptions([player.Character.CardPool], CardCreationSource.Encounter,
                CardRarityOddsType.Uniform, card => card is Finesse)
                .WithFlags(CardCreationFlags.NoCardPoolModifications);
            Assert.Throws<InvalidOperationException>(() => new CardReward(player, suppressed, 1).Populate(run));
        }
        else if (relicType == typeof(LastingCandy))
        {
            await RelicCmd.Obtain(canonical, player);
            var candy = Assert.Single(player.Relics.OfType<LastingCandy>());
            for (int combat = 0; combat < 3; combat++)
            {
                var room = NewCombat();
                await room.Enter(run);
                await CreatureCmd.Damage(room.Engine.State, room.Engine.State.Enemies.ToArray(), 9999m,
                    ValueProp.Unpowered, player.Creature, null, null);
                Assert.True(room.Engine.CheckWinCondition());
                await room.ResolveOutcomeAsync();
                var reward = Assert.Single(room.GeneratedRewards).Card;
                Assert.Equal(combat == 1 ? 4 : 3, reward.Options.Count);
                if (combat == 1) Assert.Equal(CardType.Power, reward.Options.Last().Type);
                Assert.Equal(combat + 1, candy.CombatRewardsSeen);
                await room.ResolveOutcomeAsync();
                Assert.Equal(combat + 1, candy.CombatRewardsSeen);
                await room.Exit(run);
            }
            CardModel power = player.Character.CardPool.AllCards.First(card => card.Type == CardType.Power);
            var filtered = new CardCreationOptions([player.Character.CardPool], CardCreationSource.Encounter,
                CardRarityOddsType.Uniform, card => card.Id == power.Id)
                .WithFlags(CardCreationFlags.IsFromCombat);
            var duplicateFallback = new CardReward(player, filtered, 1);
            duplicateFallback.Populate(run);
            Assert.Equal(2, duplicateFallback.Options.Count);
            Assert.All(duplicateFallback.Options, card => Assert.Equal(power.Id, card.Id));
            var skillsOnly = new CardCreationOptions([player.Character.CardPool], CardCreationSource.Encounter,
                CardRarityOddsType.Uniform, card => card.Type == CardType.Skill)
                .WithFlags(CardCreationFlags.IsFromCombat);
            var noPower = new CardReward(player, skillsOnly, 1);
            noPower.Populate(run);
            Assert.Equal(CardType.Skill, Assert.Single(noPower.Options).Type);
            Assert.Equal(3, candy.CombatRewardsSeen);
            Assert.True(candy.IsAllowed(run));
            foreach (int floor in Enumerable.Range(0, 41)) run.AddVisitedMapCoord(new MapCoord(floor, 0));
            Assert.False(candy.IsAllowed(run));
        }
        else if (relicType == typeof(MiniatureTent))
            await VerifyRestFlow(canonical);
        else if (relicType == typeof(PetrifiedToad))
        {
            await RelicCmd.Obtain(canonical, player);
            var observer = (SharedRelicPoolTask3bTests)ModelDb.Relic<SharedRelicPoolTask3bTests>().MutableClone();
            observer.AssignOwner(player);
            player.AddRelicInternal(observer);
            foreach (bool full in new[] { false, true })
            {
                foreach (PotionModel potion in player.PotionSlots.OfType<PotionModel>().ToArray()) player.RemovePotionInternal(potion);
                observer.FillSlotsEarly = full;
                var room = NewCombat();
                await room.Enter(run);
                Assert.Equal(full ? 2 : 1, observer.EarlyCalls);
                if (full) Assert.All(player.PotionSlots, potion => Assert.IsType<StrengthPotion>(potion));
                else
                {
                    var rock = Assert.IsType<PotionShapedRock>(Assert.Single(player.PotionSlots.OfType<PotionModel>()));
                    Assert.Same(player, rock.Owner);
                    int hp = room.Engine.State.Enemies[0].CurrentHp;
                    await PotionCmd.Use(rock, player, room.Engine.State.Enemies[0]);
                    Assert.Equal(15, hp - room.Engine.State.Enemies[0].CurrentHp);
                    Assert.DoesNotContain(rock, player.PotionSlots);
                }
                await room.Exit(run);
            }
        }
        else if (relicType == typeof(WingCharm))
        {
            await RelicCmd.Obtain(canonical, player);
            CardModel[] originals = [ModelDb.Card<StrikeRegent>(), ModelDb.Card<DefendRegent>(), ModelDb.Card<Dazed>()];
            var reward = new CardReward(player, originals);
            int nicheBefore = run.Rng.Niche.Counter;
            reward.Populate(run);
            Assert.Equal(3, reward.Options.Count);
            CardModel changed = Assert.Single(reward.Options, card => card.Enchantments.Count > 0);
            Assert.Equal(1m, Assert.IsType<Swift>(Assert.Single(changed.Enchantments)).Magnitude);
            Assert.Equal(1, run.Rng.Niche.Counter - nicheBefore);
            Assert.All(originals, card => Assert.Empty(card.Enchantments));
            Assert.Equal(2, reward.Options.Count(card => originals.Any(original => ReferenceEquals(card, original))));
            Assert.Same(originals[2], reward.Options[2]);
            await reward.SelectOption(changed);
            Assert.IsType<Swift>(Assert.Single(player.Deck.Cards.Last().Enchantments));
        }
    }

    private async Task VerifyMerchant(RunState run, Player player, RelicModel canonical)
    {
        var shop = new MerchantRoom();
        run.PushRoom(shop);
        await shop.EnterInternal(run);
        Purchase = new MerchantRelicEntry(canonical, 10, player);
        ((List<MerchantRelicEntry>)shop.Inventory.Relics)[0] = Purchase;
        player.Gold = 1000;
        int deckBefore = player.Deck.Cards.Count;
        await DrivePrivate(new RunDriver(run, this), "DriveShopAsync", shop);
        Assert.True(Purchase.Purchased);
        Assert.Equal(990, player.Gold);
        Assert.Equal(5, RewardChoices);
        Assert.Equal(5, Offer!.ExtraRewards.Count);
        Assert.All(Offer.ExtraRewards, reward => Assert.True(reward.IsResolved));
        if (canonical is Cauldron)
        {
            Assert.All(Offer.ExtraRewards, reward => Assert.IsType<PotionReward>(reward));
            Assert.Equal(3, player.PotionSlots.Count(potion => potion is not null));
        }
        else
        {
            Assert.All(Offer.ExtraRewards, reward =>
            {
                var cardReward = Assert.IsType<CardReward>(reward);
                Assert.Equal(3, cardReward.Options.Count);
                Assert.All(cardReward.Options, card => Assert.Contains(player.Character.CardPool.AllCards, c => c.Id == card.Id));
                Assert.NotNull(cardReward.SelectedOption);
            });
            Assert.Equal(deckBefore + 5, player.Deck.Cards.Count);
        }
        await shop.Exit(run);
    }

    private async Task VerifyRestFlow(RelicModel tentRelic)
    {
        foreach (bool tent in new[] { false, true })
        {
            RunState run = NewRun("task3b-rest-" + tent);
            Player player = run.Players[0];
            await RelicCmd.Obtain(ModelDb.Relic<Girya>(), player);
            var girya = Assert.Single(player.Relics.OfType<Girya>());
            if (tent) await RelicCmd.Obtain(tentRelic, player);
            var room = new RestSiteRoom();
            run.PushRoom(room);
            await room.EnterInternal(run);
            RestSiteDecision[] initial = room.GetAvailableDecisions(run, player).ToArray();
            int initialSmithCount = initial.OfType<RestSiteDecision.Smith>().Count();
            Assert.True(initialSmithCount > 1);
            RestChoices.Clear();
            await DrivePrivate(new RunDriver(run, this), "DriveRestSiteAsync", room);
            Assert.Equal(1, girya.TimesLifted);
            Assert.Equal(tent ? initial.Length - initialSmithCount + 1 : 1, RestChoices.Count);
            Assert.All(RestChoices, choice => Assert.Contains(initial, item => ReferenceEquals(item, choice)));
            Assert.Single(RestChoices.OfType<LiftRestSiteOption>());
            Assert.Equal(tent ? 1 : 0, RestChoices.OfType<RestSiteDecision.Smith>().Count());
            Assert.Equal(tent ? 1 : 0, player.Deck.Cards.Count(card => card.IsUpgraded));
            Assert.False(room.HasRemainingDecisions(player));
            Assert.Empty(room.GetAvailableDecisions(run, player));
            await room.Exit(run);
            Assert.Contains(room.GetAvailableDecisions(run, player), choice => choice is LiftRestSiteOption);
        }

        RunState cookRun = NewRun("task3b-tent-cook-prunes-smith");
        Player cookPlayer = cookRun.Players[0];
        cookRun.ConfigureCardSelectionSource(this);
        await RelicCmd.Obtain(tentRelic, cookPlayer);
        await RelicCmd.Obtain(ModelDb.Relic<MeatCleaver>(), cookPlayer);
        var cookRoom = new RestSiteRoom();
        await cookRoom.EnterInternal(cookRun);
        RestSiteDecision[] cookInitial = cookRoom.GetAvailableDecisions(cookRun, cookPlayer).ToArray();
        RestSiteDecision.Cook cook = Assert.Single(cookInitial.OfType<RestSiteDecision.Cook>());
        CardModel[] cookedCards = cookPlayer.Deck.Cards.Where(card => card.IsRemovable).Take(2).ToArray();

        await cookRoom.ResolveAsync(cookPlayer, cook);

        Assert.All(cookedCards, card => Assert.DoesNotContain(
            cookPlayer.Deck.Cards,
            current => ReferenceEquals(card, current)));
        RestSiteDecision.Smith[] smithsAfterCook = cookRoom.GetAvailableDecisions(cookRun, cookPlayer)
            .OfType<RestSiteDecision.Smith>()
            .ToArray();
        Assert.All(smithsAfterCook, smith =>
        {
            Assert.Contains(cookPlayer.Deck.Cards, card => ReferenceEquals(card, smith.Card));
            Assert.True(smith.Card.IsUpgradable);
        });
        Assert.DoesNotContain(
            smithsAfterCook,
            smith => cookedCards.Any(card => ReferenceEquals(card, smith.Card)));

        RunState failedRun = NewRun("task3b-tent-failed-smith");
        Player failedPlayer = failedRun.Players[0];
        await RelicCmd.Obtain(tentRelic, failedPlayer);
        var failedRoom = new RestSiteRoom();
        await failedRoom.EnterInternal(failedRun);
        RestSiteDecision.Smith disabledSmith = failedRoom.GetAvailableDecisions(failedRun, failedPlayer)
            .OfType<RestSiteDecision.Smith>()
            .First();
        while (disabledSmith.Card.IsUpgradable) disabledSmith.Card.Upgrade();
        RestSiteDecision[] beforeFailedSelection =
            failedRoom.GetAvailableDecisions(failedRun, failedPlayer).ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => failedRoom.ResolveAsync(failedPlayer, disabledSmith));

        Assert.Equal(
            beforeFailedSelection,
            failedRoom.GetAvailableDecisions(failedRun, failedPlayer));

        RunState engineRun = NewRun("task3b-tent-engine");
        Player enginePlayer = engineRun.Players[0];
        await RelicCmd.Obtain(tentRelic, enginePlayer);
        await RelicCmd.Obtain(ModelDb.Relic<Girya>(), enginePlayer);
        var engineGirya = Assert.Single(enginePlayer.Relics.OfType<Girya>());
        engineRun.Map.StartingMapPoint.PointType = MapPointType.Monster;
        engineRun.Map.StartingMapPoint.Children.OrderBy(point => point.coord.col).First().PointType = MapPointType.RestSite;
        await new RunEngine(engineRun, points => points.OrderBy(point => point.coord.col).First()).RunAsync(maxFloors: 1);
        Assert.Equal(1, engineGirya.TimesLifted);
        Assert.Single(enginePlayer.Deck.Cards, card => card.IsUpgraded);
    }

    public override Task BeforeCombatStart()
    {
        EarlyCalls++;
        Assert.DoesNotContain(Owner.PotionSlots, potion => potion is PotionShapedRock);
        if (FillSlotsEarly)
            while (Owner.PotionSlots.Contains(null)) Owner.AddPotionInternal((PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone());
        return Task.CompletedTask;
    }
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => Task.FromResult(options[0]);
    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => throw new NotSupportedException();
    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
    {
        Selection = request;
        return Task.FromResult<IReadOnlyList<CardModel>>(request.Candidates.Take(request.MaxCount).ToArray());
    }
    public Task<RestSiteDecision> ChooseRestSiteActionAsync(Player player, IReadOnlyList<RestSiteDecision> candidates)
    {
        RestSiteDecision choice = candidates.OfType<LiftRestSiteOption>().FirstOrDefault() ?? candidates[0];
        Assert.DoesNotContain(RestChoices, previous => ReferenceEquals(previous, choice));
        RestChoices.Add(choice);
        return Task.FromResult(choice);
    }
    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player)
    {
        if (Purchase is { Purchased: false }) return Task.FromResult<ShopDecision>(new ShopDecision.BuyRelic(Purchase));
        Assert.All(Offer!.ExtraRewards, reward => Assert.True(reward.IsResolved));
        return Task.FromResult<ShopDecision>(new ShopDecision.Leave());
    }
    public async Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
    {
        Offer = rewards;
        var shop = Assert.IsType<MerchantRoom>(rewards.Player.RunState.CurrentRoom);
        RewardDecision next = RewardDecisionClassifier.ChooseDefault(rewards);
        if (next is RewardDecision.Done) return next;
        Assert.True(shop.HasPendingRewards);
        if (RewardChoices == 0)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => shop.Exit((RunState)rewards.Player.RunState));
            await Assert.ThrowsAsync<InvalidOperationException>(() => shop.Buy(shop.Inventory.Cards[0], rewards.Player));
        }
        if (RewardDecisionClassifier.Classify(rewards) is RewardDecisionClassification.Choice choice)
        {
            RewardChoices++;
            if (choice.Candidates[0] is RewardDecision.ResolveExtra { Reward: PotionReward { CanTake: false } full })
            {
                await full.Take();
                Assert.False(full.IsResolved);
            }
        }
        return next;
    }
    private static RunState NewRun(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        run.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Regent>(), run));
        return run;
    }
    private static CombatRoom NewCombat() => new(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
    private static Task DrivePrivate(RunDriver driver, string method, AbstractRoom room) =>
        (Task)typeof(RunDriver).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(driver, [room])!;
}
