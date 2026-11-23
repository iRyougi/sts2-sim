using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

file sealed class RoomMerchantCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;
    public override IReadOnlyList<Type> StartingDeck => ModelDb.Character<Regent>().StartingDeck;
    public override IReadOnlyList<Type> StartingRelics => ModelDb.Character<Regent>().StartingRelics;
    public override Sts2Sim.Core.Models.CardPools.CardPoolModel CardPool => new RoomMerchantPool();
}

file sealed class RoomMerchantPool : Sts2Sim.Core.Models.CardPools.CardPoolModel
{
    public override IReadOnlyList<CardModel> AllCards => [ModelDb.Card<MerchantTestAttack>()];
}
file sealed class MerchantTestAttack : CardModel
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;
}

[Collection("ModelDb")]
public sealed class MerchantRoomTests : IDisposable
{
    public MerchantRoomTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(RoomMerchantCharacter), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight),
            typeof(MerchantTestAttack), typeof(Vajra), typeof(Circlet), typeof(StrengthPotion),
            typeof(Sozu), typeof(TheCourier), typeof(PurchaseHookProbe), typeof(AscendersBane),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EnterInternal_GeneratesInventoryForTheRunPlayer()
    {
        (RunState runState, _) = CreateRun("merchant-room-inventory", useTestPool: true);
        var room = new MerchantRoom();

        await room.EnterInternal(runState);

        Assert.Single(room.Inventory.Cards);
        Assert.Equal(3, room.Inventory.Relics.Count);
        Assert.InRange(room.Inventory.Potions.Count, 0, 3);
        // Inflation 修复：A0 移牌基础价为 75。
        Assert.Equal(75, room.Inventory.CardRemoval.Price);
    }

    [Fact]
    public async Task Buy_Card_DeductsGoldAndAddsAnOwnedClone()
    {
        (RunState runState, Player player) = CreateRun("merchant-room-card", useTestPool: true);
        player.Gold = 99999;
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        MerchantCardEntry entry = room.Inventory.Cards[0];
        int goldBefore = player.Gold;

        await room.Buy(entry, player);

        CardModel purchased = Assert.Single(player.Deck.Cards, card => card.Id == entry.Card.Id);
        Assert.NotSame(entry.Card, purchased);
        Assert.Same(player, purchased.Owner);
        Assert.Equal(goldBefore - entry.Price, player.Gold);
        Assert.True(entry.Purchased);
    }

    [Fact]
    public async Task Buy_Relic_RejectsAnEntryFromAnotherRoomWithoutMutatingEitherSide()
    {
        (RunState firstRun, Player player) = CreateRun("merchant-room-membership-player");
        player.Gold = 99999;
        var currentRoom = new MerchantRoom();
        await currentRoom.EnterInternal(firstRun);

        (RunState otherRun, _) = CreateRun("merchant-room-membership-entry");
        var otherRoom = new MerchantRoom();
        await otherRoom.EnterInternal(otherRun);
        MerchantRelicEntry foreignEntry = otherRoom.Inventory.Relics[0];
        int goldBefore = player.Gold;
        int relicCountBefore = player.Relics.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => currentRoom.Buy(foreignEntry, player));

        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(relicCountBefore, player.Relics.Count);
        Assert.False(foreignEntry.Purchased);
    }

    [Fact]
    public async Task Buy_Relic_RejectsPlayerFromAnotherRunWithoutMutatingEitherSide()
    {
        (RunState currentRun, _) = CreateRun("merchant-room-cross-run-current");
        var room = new MerchantRoom();
        await room.EnterInternal(currentRun);
        MerchantRelicEntry entry = room.Inventory.Relics[0];

        (_, Player otherRunPlayer) = CreateRun("merchant-room-cross-run-other");
        otherRunPlayer.Gold = 99999;
        int goldBefore = otherRunPlayer.Gold;
        int relicCountBefore = otherRunPlayer.Relics.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => room.Buy(entry, otherRunPlayer));

        Assert.Equal(goldBefore, otherRunPlayer.Gold);
        Assert.Equal(relicCountBefore, otherRunPlayer.Relics.Count);
        Assert.False(entry.Purchased);
    }

    [Fact]
    public async Task BuyCardRemoval_UsesAnotherPlayersOwnInventoryWithoutMutatingThePresentationInventory()
    {
        (RunState runState, Player inventoryPlayer) = CreateRun("merchant-room-same-run-other-player");
        var otherPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(otherPlayer);
        otherPlayer.Gold = 99999;
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        CardModel card = otherPlayer.Deck.Cards[0];
        int goldBefore = otherPlayer.Gold;
        int ownRemovalPrice = room.InventoryFor(otherPlayer).CardRemoval.Price;

        await room.BuyCardRemoval(card, otherPlayer);

        Assert.Equal(goldBefore - ownRemovalPrice, otherPlayer.Gold);
        Assert.DoesNotContain(otherPlayer.Deck.Cards, candidate => ReferenceEquals(candidate, card));
        Assert.Equal(1, otherPlayer.CardRemovalsUsed);
        Assert.False(room.Inventory.CardRemoval.Purchased);
        Assert.True(room.InventoryFor(otherPlayer).CardRemoval.Purchased);
        Assert.NotSame(otherPlayer, inventoryPlayer);
    }

    [Fact]
    public async Task EnterInternal_ReentryRebindsInventoryToTheNewRunPlayer()
    {
        (RunState firstRun, Player firstPlayer) = CreateRun("merchant-room-reentry-first");
        firstPlayer.Gold = 99999;
        var room = new MerchantRoom();
        await room.EnterInternal(firstRun);
        MerchantInventory firstInventory = room.Inventory;

        (RunState secondRun, Player secondPlayer) = CreateRun("merchant-room-reentry-second");
        secondPlayer.Gold = 99999;
        await room.EnterInternal(secondRun);
        MerchantRelicEntry currentEntry = room.Inventory.Relics[0];

        await Assert.ThrowsAsync<InvalidOperationException>(() => room.Buy(currentEntry, firstPlayer));
        await room.Buy(currentEntry, secondPlayer);

        Assert.NotSame(firstInventory, room.Inventory);
        Assert.True(currentEntry.Purchased);
        Assert.False(firstInventory.Relics[0].Purchased);
    }

    [Fact]
    public async Task Buy_Relic_CannotBePurchasedTwiceWithoutAdditionalMutation()
    {
        (RunState runState, Player player) = CreateRun("merchant-room-idempotent");
        player.Gold = 99999;
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        MerchantRelicEntry entry = room.Inventory.Relics[0];

        await room.Buy(entry, player);

        int goldAfterFirstPurchase = player.Gold;
        int relicCountAfterFirstPurchase = player.Relics.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => room.Buy(entry, player));

        Assert.Equal(goldAfterFirstPurchase, player.Gold);
        Assert.Equal(relicCountAfterFirstPurchase, player.Relics.Count);
        Assert.True(entry.Purchased);
    }

    [Fact]
    public async Task Buy_Relic_RejectsInsufficientFundsWithoutMarkingTheEntrySold()
    {
        (RunState runState, Player player) = CreateRun("merchant-room-funds");
        player.Gold = 0;
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        MerchantRelicEntry entry = room.Inventory.Relics[0];
        int relicCountBefore = player.Relics.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => room.Buy(entry, player));

        Assert.Equal(0, player.Gold);
        Assert.Equal(relicCountBefore, player.Relics.Count);
        Assert.False(entry.Purchased);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Buy_Potion_FailureLeavesGoldShelfSlotsHooksAndRngUnchanged(bool useSozu)
    {
        const string seed = "merchant-room-potion-failure";
        (RunState runState, Player player) = CreateRun(seed, ascensionLevel: 10);
        Assert.Equal(2, player.MaxPotionCount);
        player.Gold = 99999;
        var probe = (PurchaseHookProbe)ModelDb.Relic<PurchaseHookProbe>().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        var courier = (TheCourier)ModelDb.Relic<TheCourier>().MutableClone();
        courier.AssignOwner(player);
        player.AddRelicInternal(courier);
        if (useSozu)
        {
            var sozu = (Sozu)ModelDb.Relic<Sozu>().MutableClone();
            sozu.AssignOwner(player);
            player.AddRelicInternal(sozu);
        }
        else
        {
            for (int index = 0; index < player.MaxPotionCount; index++)
                player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        }
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        MerchantPotionEntry entry = room.Inventory.Potions[0];
        PotionModel shelfPotion = entry.Potion;
        var shelvesBefore = room.Inventory.Potions.ToArray();
        var slotsBefore = player.PotionSlots.ToArray();
        var runCounters = Enum.GetValues<RunRngType>().Select(type => runState.Rng.GetRng(type).Counter).ToArray();
        var playerCounters = Enum.GetValues<PlayerRngType>().Select(type => player.PlayerRng.GetRng(type).Counter).ToArray();
        int goldBefore = player.Gold;
        int priceBefore = entry.Price;

        var (success, price) = await room.TryBuyPotionWithPriceAsync(entry, player);

        Assert.False(success);
        Assert.Equal(0, price);

        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(priceBefore, entry.Price);
        Assert.Equal(shelvesBefore, room.Inventory.Potions);
        Assert.Equal(slotsBefore, player.PotionSlots);
        Assert.Same(shelfPotion, entry.Potion);
        Assert.False(entry.Purchased);
        Assert.Null(shelfPotion.Owner);
        Assert.Equal(1, probe.ProcurementChecks);
        Assert.Equal(0, probe.PotionsProcured);
        Assert.Equal(0, probe.ItemsPurchased);
        Assert.Equal(0, probe.RefillChecks);
        Assert.Equal(runCounters, Enum.GetValues<RunRngType>().Select(type => runState.Rng.GetRng(type).Counter));
        Assert.Equal(playerCounters, Enum.GetValues<PlayerRngType>().Select(type => player.PlayerRng.GetRng(type).Counter));
    }

    [Fact]
    public async Task Buy_Potion_AddsTheShelfPotionWithThePlayerAsOwner()
    {
        (RunState runState, Player player) = CreateRun("merchant-room-potion-owner");
        player.Gold = 99999;
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        MerchantPotionEntry entry = room.Inventory.Potions[0];

        await room.Buy(entry, player);

        Assert.Same(entry.Potion, player.PotionSlots.Single(potion => potion is not null));
        Assert.Same(player, entry.Potion.Owner);
        Assert.True(entry.Purchased);
    }

    [Fact]
    public async Task BuyCardRemoval_RejectsACardOutsideTheDeckWithoutMutatingPurchaseState()
    {
        (RunState runState, Player player) = CreateRun("merchant-room-remove-membership");
        player.Gold = 99999;
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        CardModel cardOutsideDeck = (CardModel)ModelDb.Card<MerchantTestAttack>().MutableClone();
        int goldBefore = player.Gold;
        int deckCountBefore = player.Deck.Cards.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => room.BuyCardRemoval(cardOutsideDeck, player));

        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(deckCountBefore, player.Deck.Cards.Count);
        Assert.Equal(0, player.CardRemovalsUsed);
        Assert.False(room.Inventory.CardRemoval.Purchased);
    }

    [Fact]
    public async Task BuyCardRemoval_RemovesTheDeckCardAndCannotBePurchasedTwice()
    {
        (RunState runState, Player player) = CreateRun("merchant-room-remove");
        player.Gold = 99999;
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        CardModel card = player.Deck.Cards[0];
        int goldBefore = player.Gold;

        await room.BuyCardRemoval(card, player);

        Assert.DoesNotContain(player.Deck.Cards, deckCard => ReferenceEquals(card, deckCard));
        Assert.Equal(goldBefore - room.Inventory.CardRemoval.Price, player.Gold);
        Assert.Equal(1, player.CardRemovalsUsed);
        Assert.True(room.Inventory.CardRemoval.Purchased);

        int goldAfterFirstPurchase = player.Gold;
        await Assert.ThrowsAsync<InvalidOperationException>(() => room.BuyCardRemoval(player.Deck.Cards[0], player));

        Assert.Equal(goldAfterFirstPurchase, player.Gold);
        Assert.Equal(1, player.CardRemovalsUsed);
    }

    private static (RunState runState, Player player) CreateRun(string seed, bool useTestPool = false, int ascensionLevel = 0)
    {
        var runState = new RunState(seed, new Overgrowth(), ascensionLevel);
        Player player = Player.CreateForNewRun(useTestPool ? ModelDb.Character<RoomMerchantCharacter>() : ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    [Fact]
    public async Task Buy_AfterExitRejectsWithoutMutation()
    {
        (RunState runState, Player player) = CreateRun("merchant-room-after-exit");
        player.Gold = 99999;
        var room = new MerchantRoom();
        await room.EnterInternal(runState);
        MerchantRelicEntry entry = room.Inventory.Relics[0];
        int goldBefore = player.Gold;
        int relicCountBefore = player.Relics.Count;
        await room.Exit(runState);

        await Assert.ThrowsAsync<InvalidOperationException>(() => room.Buy(entry, player));

        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(relicCountBefore, player.Relics.Count);
        Assert.False(entry.Purchased);
    }

    [Fact]
    public async Task Buy_FromRetainedOldRoomRejectsWhenAnotherRoomIsCurrent()
    {
        (RunState runState, Player player) = CreateRun("merchant-room-stale-current");
        player.Gold = 99999;
        var oldRoom = new MerchantRoom();
        await oldRoom.EnterInternal(runState);
        MerchantRelicEntry oldEntry = oldRoom.Inventory.Relics[0];
        var currentRoom = new MerchantRoom();
        await currentRoom.EnterInternal(runState);
        runState.PushRoom(currentRoom);
        MerchantRelicEntry currentEntry = currentRoom.Inventory.Relics[0];

        Task stalePurchase = oldRoom.Buy(oldEntry, player);
        Task currentPurchase = currentRoom.Buy(currentEntry, player);
        await Assert.ThrowsAsync<InvalidOperationException>(() => stalePurchase);
        await currentPurchase;

        Assert.False(oldEntry.Purchased);
        Assert.True(currentEntry.Purchased);
    }
    private sealed class PurchaseHookProbe : RelicModel
    {
        public override Sts2Sim.Core.Entities.Relics.RelicRarity Rarity => Sts2Sim.Core.Entities.Relics.RelicRarity.Common;
        public int ProcurementChecks { get; private set; }
        public int PotionsProcured { get; private set; }
        public int ItemsPurchased { get; private set; }
        public int RefillChecks { get; private set; }

        public override bool ShouldProcurePotion(PotionModel potion, Player player)
        {
            ProcurementChecks++;
            return true;
        }

        public override Task AfterPotionProcured(PotionModel potion)
        {
            PotionsProcured++;
            return Task.CompletedTask;
        }

        public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
        {
            ItemsPurchased++;
            return Task.CompletedTask;
        }

        public override bool ShouldRefillMerchantEntry(MerchantEntry entry, Player player)
        {
            RefillChecks++;
            return false;
        }
    }
}
