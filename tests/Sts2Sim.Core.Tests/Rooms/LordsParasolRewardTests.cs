using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

// Test-only stock setup and observer; purchase and reward effects use real commands.
[Collection("ModelDb")]
public sealed class LordsParasolRewardTests : RelicModel, IRunDecisionSource, IDisposable
{
    public override RelicRarity Rarity => RelicRarity.Common;
    private Type? _rewardRelic;
    private string _scenario = "empty";
    private MerchantRoom? _shop;
    private MerchantInventory? _stock;
    private MerchantRelicEntry? _rewardEntry;
    private readonly List<Reward> _offered = [];
    private readonly List<string> _trace = [];
    private readonly Dictionary<PotionModel, int> _shelfAttempts = [];
    private CardModel? _removed;
    private int _goldBefore;
    private int _deckBefore;
    private bool _checkedReentry;

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, typeof(Orrery))]
    [InlineData(true, typeof(Orrery))]
    [InlineData(false, typeof(Cauldron))]
    [InlineData(true, typeof(Cauldron))]
    public async Task EntryRewards_CompleteBeforePurchasesContinue(bool useEngine, Type rewardRelic)
    {
        (RunState run, LordsParasolRewardTests probe) = await Setup(rewardRelic, rewardRelic == typeof(Cauldron) ? "full" : "empty");
        if (useEngine)
            await new RunEngine(run, points => points.OrderBy(point => point.coord.col).First()).RunAsync(maxFloors: 1);
        else
            await new RunDriver(run, probe).RunAsync(maxFloors: 1);

        MerchantInventory stock = probe._stock!;
        Assert.Equal(5, probe._offered.Count);
        Assert.All(probe._offered, reward => Assert.True(reward.IsResolved));
        Assert.False(probe._shop.HasPendingRewards);
        Assert.All(stock.Relics, entry => Assert.True(entry.Purchased));
        Assert.True(stock.CardRemoval.Purchased);
        Assert.Equal(probe._goldBefore, run.Players[0].Gold);
        Assert.DoesNotContain(run.Players[0].Deck.Cards, card => ReferenceEquals(card, probe._removed));
        Assert.Equal("removal-purchased", probe._trace.Last());
        Assert.True(probe._trace.IndexOf("rewards-complete") < probe._trace.IndexOf("reward-purchased"));
        int gained = rewardRelic == typeof(Orrery) ? 5 : 0;
        Assert.Equal(probe._deckBefore + stock.Cards.Count + gained - 1, run.Players[0].Deck.Cards.Count);
        Assert.All(probe._shelfAttempts.Values, attempts => Assert.Equal(1, attempts));
        if (rewardRelic == typeof(Cauldron))
            Assert.All(stock.Potions, entry => Assert.False(entry.Purchased));
        if (!useEngine) Assert.True(probe._checkedReentry);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("full")]
    [InlineData("sozu")]
    [InlineData("fault")]
    public async Task EntryScope_PreservesProcurementAndFailureBoundaries(string scenario)
    {
        (RunState run, LordsParasolRewardTests probe) = await Setup(scenario == "fault" ? typeof(Orrery) : null, scenario);
        Player player = run.Players[0];
        var driver = new RunDriver(run, probe);
        if (scenario == "fault")
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() => driver.RunAsync(maxFloors: 1));
            Assert.Equal("entry reward choice failed", failure.Message);
            Assert.True(probe._checkedReentry);
            Assert.True(probe._shop!.HasPendingRewards);
            Assert.False(probe._rewardEntry!.Purchased);
            Assert.False(probe._stock!.CardRemoval.Purchased);
            Assert.Equal(probe._goldBefore, player.Gold);
            ImaginationHiddenStateRegistry.ValidateCoverage();
            Assert.NotSame(run, run.CloneExact()); // callback scope and purchase gate both unwound
            Assert.True(probe._shop.TryDequeuePendingRewardOffer(out RewardsSet? preserved));
            await preserved!.Gold.Take();
            foreach (CardReward reward in probe._offered.OfType<CardReward>()) await reward.Skip();
            Assert.False(probe._shop.HasPendingRewards);
            var ordinary = new CardReward(player, new CardCreationOptions([player.Character.CardPool],
                CardCreationSource.Other, CardRarityOddsType.RegularEncounter), 3);
            await RewardsCmd.OfferCustom(player, [ordinary]);
            Assert.False(ordinary.IsResolved); // no leaked entry resolver on a later ordinary offer
            await Assert.ThrowsAsync<InvalidOperationException>(() => probe._shop.BuyCardRemoval(player.Deck.Cards[0], player));
            await Assert.ThrowsAsync<InvalidOperationException>(() => probe._shop.Exit(run));
            Assert.True(probe._shop.TryDequeuePendingRewardOffer(out RewardsSet? ordinarySet));
            await ordinarySet!.Gold.Take();
            await ordinary.Skip();
            await probe._shop.Exit(run);
            return;
        }

        await driver.RunAsync(maxFloors: 1);
        MerchantInventory stock = probe._stock!;
        Assert.Equal(3, probe._shelfAttempts.Count);
        Assert.All(probe._shelfAttempts.Values, attempts => Assert.Equal(1, attempts));
        int bought = scenario == "empty" ? player.PotionSlots.Count : 0;
        Assert.Equal(bought, stock.Potions.Count(entry => entry.Purchased));
        Assert.Equal(bought, probe._trace.Count(item => item == "potion-purchased"));
        Assert.All(stock.Potions.Where(entry => !entry.Purchased), entry => Assert.Null(entry.Potion.Owner));
        Assert.Equal(probe._goldBefore, player.Gold);
        Assert.True(stock.CardRemoval.Purchased);
        Assert.Equal(1, player.CardRemovalsUsed);
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, probe._removed));
    }

    private static async Task<(RunState, LordsParasolRewardTests)> Setup(Type? rewardRelic, string scenario)
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(LordsParasolRewardTests)));
        var run = new RunState("lords-entry-" + scenario, new Overgrowth(), ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        await RelicCmd.Obtain(ModelDb.Relic<LordsParasolRewardTests>(), player);
        var probe = player.Relics.OfType<LordsParasolRewardTests>().Single();
        probe._rewardRelic = rewardRelic;
        probe._scenario = scenario;
        probe._removed = player.Deck.Cards.First(card => card.IsRemovable);
        probe._goldBefore = player.Gold;
        probe._deckBefore = player.Deck.Cards.Count;
        await RelicCmd.Obtain(ModelDb.Relic<LordsParasol>(), player);
        if (scenario == "sozu") await RelicCmd.Obtain(ModelDb.Relic<Sozu>(), player);
        if (scenario == "full")
            while (player.PotionSlots.Contains(null))
                player.AddPotionInternal((PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone());
        run.Map.StartingMapPoint.PointType = MapPointType.Monster;
        foreach (MapPoint point in run.Map.StartingMapPoint.Children) point.PointType = MapPointType.Shop;
        run.ConfigureCardSelectionSource(probe);
        return (run, probe);
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not MerchantRoom shop) return Task.CompletedTask;
        _shop = shop;
        _stock = shop.Inventory;
        var relics = (List<MerchantRelicEntry>)shop.Inventory.Relics;
        relics.Clear();
        relics.Add(new MerchantRelicEntry((RelicModel)ModelDb.Get(_rewardRelic ?? typeof(Vajra)), 0, Owner));
        relics.Add(new MerchantRelicEntry(ModelDb.Relic<Anchor>(), 0, Owner));
        relics.Add(new MerchantRelicEntry(ModelDb.Relic<BagOfPreparation>(), 0, Owner));
        _rewardEntry = _rewardRelic is null ? null : relics[0];
        var potions = (List<MerchantPotionEntry>)shop.Inventory.Potions;
        potions.Clear();
        for (int index = 0; index < 3; index++)
        {
            var potion = (PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone();
            potions.Add(new MerchantPotionEntry(potion, 0, Owner));
            _shelfAttempts.Add(potion, 0);
        }
        return Task.CompletedTask;
    }

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (ReferenceEquals(player, Owner) && ReferenceEquals(player.RunState.CurrentRoom, _shop))
            _offered.AddRange(rewards);
    }

    public override bool ShouldProcurePotion(PotionModel potion, Player player)
    {
        if (_shelfAttempts.ContainsKey(potion)) _shelfAttempts[potion]++;
        return true;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (_offered.OfType<CardReward>().Any(reward => reward.Options.Any(option => option.Id == card.Id)))
        {
            Assert.False(_rewardEntry!.Purchased);
            Assert.False(_stock!.CardRemoval.Purchased);
            _trace.Add("reward-card");
        }
        return Task.CompletedTask;
    }
    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        Assert.Equal(0, goldSpent);
        if (ReferenceEquals(itemPurchased, _rewardEntry))
        {
            Assert.All(_offered, reward => Assert.True(reward.IsResolved));
            Assert.False(_shop!.HasPendingRewards);
            _trace.Add("rewards-complete");
            _trace.Add("reward-purchased");
        }
        if (itemPurchased is MerchantPotionEntry) _trace.Add("potion-purchased");
        if (itemPurchased is MerchantCardRemovalEntry) _trace.Add("removal-purchased");
        return Task.CompletedTask;
    }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(point => point.coord.col).First());
    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => throw new NotSupportedException();
    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
    {
        if (request.Source is LordsParasol && _rewardEntry is not null)
            Assert.All(_offered, reward => Assert.True(reward.IsResolved));
        return Task.FromResult<IReadOnlyList<CardModel>>(request.Candidates.Take(request.MinCount).ToArray());
    }

    public async Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
    {
        Assert.False(_rewardEntry!.Purchased);
        Assert.False(_stock!.CardRemoval.Purchased);
        RewardDecision next = RewardDecisionClassifier.ChooseDefault(rewards);
        Assert.Equal(next is not RewardDecision.Done, _shop.HasPendingRewards);
        if (!_checkedReentry)
        {
            _checkedReentry = true;
            // A timeout only prevents a broken gate hanging the test; it is never a passing result.
            await Assert.ThrowsAsync<InvalidOperationException>(() => _shop.Buy(_stock!.Potions[0], Owner).WaitAsync(TimeSpan.FromSeconds(3)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => _shop.Exit((RunState)Owner.RunState).WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(RunCloneRejectionReason.PendingCallback,
                Assert.Throws<RunCloneNotSupportedException>(() => ((RunState)Owner.RunState).CloneExact()).Reason);
        }
        await Task.Yield(); // ensure the purchase really awaits an asynchronous choice
        if (_scenario == "fault") throw new InvalidOperationException("entry reward choice failed");
        return next;
    }
}
