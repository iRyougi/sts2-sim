using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Rewards;
using System.Diagnostics.CodeAnalysis;

namespace Sts2Sim.Core.Rooms;

/// <summary>Merchant room inventory and purchase operations.</summary>
public sealed class MerchantRoom : AbstractRoom
{
    private readonly SemaphoreSlim _purchaseGate = new(1, 1);
    private readonly Dictionary<Player, MerchantInventory> _inventories = new();
    private RunState? _inventoryRunState;
    private Player? _displayPlayer;
    private bool _isActive;
    private readonly object _rewardLock = new();
    private readonly Queue<RewardsSet> _rewardOffers = new();

    public bool HasPendingRewards
    {
        get { lock (_rewardLock) return _rewardOffers.Any(HasUnresolvedRewards); }
    }

    internal bool CanOfferRewards(Player player) => _isActive &&
        ReferenceEquals(_inventoryRunState, player.RunState) && _inventories.ContainsKey(player) &&
        ReferenceEquals(player.RunState.CurrentRoom, this);

    internal bool TryOfferRewardsFromCurrentMerchant(IRunState runState, RewardsSet rewards)
    {
        if (!ReferenceEquals(runState, rewards.Player.RunState) || !CanOfferRewards(rewards.Player)) return false;
        lock (_rewardLock) _rewardOffers.Enqueue(rewards);
        return true;
    }

    internal bool TryDequeuePendingRewardOffer([NotNullWhen(true)] out RewardsSet? rewards)
    {
        lock (_rewardLock)
        {
            while (_rewardOffers.TryPeek(out var first) && !HasUnresolvedRewards(first)) _rewardOffers.Dequeue();
            // Keep the active offer until resolved so direct purchases and exits remain gated.
            return _rewardOffers.TryPeek(out rewards);
        }
    }

    private static bool HasUnresolvedRewards(RewardsSet rewards) => !rewards.Gold.IsResolved ||
        !rewards.Card.IsResolved || rewards.Potion is { IsResolved: false } ||
        rewards.Relic is { IsResolved: false } || rewards.ExtraRewards.Any(reward => !reward.IsResolved);

    public override RoomType RoomType => RoomType.Shop;

    /// <summary>Headless equivalent of the shop window covering the merchant target button.</summary>
    public bool IsInventoryOpen { get; set; }

    public bool IsMerchantTargetAvailable(Player player) => _isActive && !IsInventoryOpen && !HasPendingRewards &&
        ReferenceEquals(_inventoryRunState, player.RunState) && _inventories.ContainsKey(player) &&
        ReferenceEquals(player.RunState.CurrentRoom, this);

    public override ModelId? ModelId => null;

    /// <summary>Presentation inventory remains the first player, preserving the existing single-inventory decision API.</summary>
    public MerchantInventory Inventory => InventoryFor(_displayPlayer
        ?? throw new InvalidOperationException("Merchant inventory has not been generated."));

    /// <summary>Gets the stock generated for one player when this merchant was entered.</summary>
    public MerchantInventory InventoryFor(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return _inventories.GetValueOrDefault(player)
            ?? throw new InvalidOperationException("Merchant inventory has not been generated for this player.");
    }

    public override async Task EnterInternal(RunState? runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        await _purchaseGate.WaitAsync();
        try
        {
            if (HasPendingRewards) throw new InvalidOperationException("Cannot reenter a merchant with unresolved rewards.");
            lock (_rewardLock) _rewardOffers.Clear();
            _inventories.Clear();
            foreach (Player player in runState.Players)
            {
                _inventories.Add(player, MerchantInventory.Generate(player));
            }
            _inventoryRunState = runState;
            _displayPlayer = runState.Players[0];
            _isActive = true;
        }
        finally
        {
            _purchaseGate.Release();
        }
    }

    public Task Buy(MerchantCardEntry entry, Player player) => BuyWithPriceAsync(entry, player);

    /// <summary>Buys a card and returns the charged price for reporting consumers.</summary>
    public async Task<int> BuyWithPriceAsync(MerchantCardEntry entry, Player player)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(player);
        await _purchaseGate.WaitAsync();
        try
        {
            MerchantInventory inventory = InventoryFor(player);
            ValidatePurchase(entry, player, inventory.Cards);
            int price = entry.Price;

            var card = (CardModel)entry.Card.MutableClone();
            card.AssignOwner(player);
            await CardPileCmd.AddToDeck(card);
            await PlayerCmd.LoseGold(price, player);
            entry.MarkPurchased();
            RefillIfNeeded(entry, player, inventory);
            await Hook.AfterItemPurchased(player.RunState, player, entry, price);
            inventory.RefreshCardCreationResults();
            return price;
        }
        finally
        {
            _purchaseGate.Release();
        }
    }

    public Task Buy(MerchantRelicEntry entry, Player player) => BuyWithPriceAsync(entry, player);

    /// <summary>Buys a relic and returns the charged price for reporting consumers.</summary>
    public async Task<int> BuyWithPriceAsync(MerchantRelicEntry entry, Player player)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(player);
        await _purchaseGate.WaitAsync();
        try
        {
            MerchantInventory inventory = InventoryFor(player);
            ValidatePurchase(entry, player, inventory.Relics);
            int price = entry.Price;

            await RelicCmd.Obtain(entry.Relic, player);
            await PlayerCmd.LoseGold(price, player);
            entry.MarkPurchased();
            RefillIfNeeded(entry, player, inventory);
            await Hook.AfterItemPurchased(player.RunState, player, entry, price);
            inventory.RefreshCardCreationResults();
            return price;
        }
        finally
        {
            _purchaseGate.Release();
        }
    }

    public Task Buy(MerchantPotionEntry entry, Player player) => BuyWithPriceAsync(entry, player);

    /// <summary>Buys a potion and returns the charged price for reporting consumers.</summary>
    public async Task<int> BuyWithPriceAsync(MerchantPotionEntry entry, Player player)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(player);
        await _purchaseGate.WaitAsync();
        try
        {
            MerchantInventory inventory = InventoryFor(player);
            ValidatePurchase(entry, player, inventory.Potions);
            if (!player.PotionSlots.Contains(null))
            {
                throw new InvalidOperationException("No empty potion slot available.");
            }

            if (entry.Potion.Owner is not null)
            {
                throw new InvalidOperationException("The merchant potion already has an owner.");
            }

            int price = entry.Price;
            if (!await PotionCmd.TryToProcure(entry.Potion, player))
            {
                throw new InvalidOperationException("Potion acquisition was vetoed.");
            }
            await PlayerCmd.LoseGold(price, player);
            entry.MarkPurchased();
            RefillIfNeeded(entry, player, inventory);
            await Hook.AfterItemPurchased(player.RunState, player, entry, price);
            inventory.RefreshCardCreationResults();
            return price;
        }
        finally
        {
            _purchaseGate.Release();
        }
    }

    public Task BuyCardRemoval(CardModel cardToRemove, Player player) =>
        BuyCardRemovalWithPriceAsync(cardToRemove, player);

    /// <summary>Buys card removal and returns the charged price for reporting consumers.</summary>
    public async Task<int> BuyCardRemovalWithPriceAsync(CardModel cardToRemove, Player player)
    {
        ArgumentNullException.ThrowIfNull(cardToRemove);
        ArgumentNullException.ThrowIfNull(player);
        await _purchaseGate.WaitAsync();
        try
        {
            MerchantCardRemovalEntry entry = InventoryFor(player).CardRemoval;
            ValidatePurchase(entry, player, new[] { entry });
            if (!player.Deck.Cards.Contains(cardToRemove))
            {
                throw new InvalidOperationException("Card is not in the player's deck.");
            }
            if (!cardToRemove.IsRemovable)
            {
                throw new InvalidOperationException("Eternal cards cannot be removed from the persistent deck.");
            }

            int price = entry.Price;
            await CardPileCmd.RemoveFromDeck(player, cardToRemove);
            player.IncrementCardRemovalsUsed();
            await PlayerCmd.LoseGold(price, player);
            entry.MarkPurchased();
            await Hook.AfterItemPurchased(player.RunState, player, entry, price);
            InventoryFor(player).RefreshCardCreationResults();
            return price;
        }
        finally
        {
            _purchaseGate.Release();
        }
    }

    private void RefillIfNeeded(MerchantEntry entry, Player player, MerchantInventory inventory)
    {
        if (Hook.ShouldRefillMerchantEntry(player.RunState, entry, player))
        {
            inventory.Restock(entry);
        }
    }

    private void ValidatePurchase<TEntry>(TEntry entry, Player player, IEnumerable<TEntry> inventory)
        where TEntry : MerchantEntry
    {
        if (HasPendingRewards) throw new InvalidOperationException("Resolve merchant rewards before purchasing another item.");
        if (!_isActive ||
            !ReferenceEquals(_inventoryRunState, player.RunState) ||
            !_inventories.ContainsKey(player) ||
            (_inventoryRunState.CurrentRoom is not null && !ReferenceEquals(_inventoryRunState.CurrentRoom, this)))
        {
            throw new InvalidOperationException("This player cannot purchase from the current merchant inventory.");
        }

        if (!inventory.Any(candidate => ReferenceEquals(candidate, entry)))
        {
            throw new InvalidOperationException("This item is not in the current merchant inventory.");
        }

        if (entry.Purchased)
        {
            throw new InvalidOperationException("This item has already been purchased.");
        }

        if (player.Gold < entry.Price)
        {
            throw new InvalidOperationException("Not enough gold for this purchase.");
        }
    }

    public override async Task Exit(RunState? runState)
    {
        await _purchaseGate.WaitAsync();
        try
        {
            if (runState is not null && !ReferenceEquals(_inventoryRunState, runState))
            {
                throw new InvalidOperationException("Cannot exit a merchant from another run.");
            }

            if (HasPendingRewards) throw new InvalidOperationException("Cannot leave a merchant with unresolved rewards.");
            lock (_rewardLock) _rewardOffers.Clear();

            _isActive = false;
            _inventoryRunState = null;
            _displayPlayer = null;
            _inventories.Clear();
        }
        finally
        {
            _purchaseGate.Release();
        }
    }
}
