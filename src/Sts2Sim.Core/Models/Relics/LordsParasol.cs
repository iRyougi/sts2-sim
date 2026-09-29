using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LordsParasol : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal originalPrice) =>
        ReferenceEquals(player, Owner) ? 0m : originalPrice;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not MerchantRoom merchant)
        {
            return;
        }

        MerchantInventory inventory = merchant.InventoryFor(Owner);
        foreach (MerchantCardEntry entry in inventory.Cards.ToArray())
        {
            if (!entry.Purchased)
            {
                await merchant.Buy(entry, Owner);
            }
        }
        foreach (MerchantRelicEntry entry in inventory.Relics.ToArray())
        {
            if (!entry.Purchased)
            {
                await merchant.Buy(entry, Owner);
            }
        }
        foreach (MerchantPotionEntry entry in inventory.Potions.ToArray())
        {
            if (!entry.Purchased && Owner.PotionSlots.Contains(null))
            {
                await merchant.Buy(entry, Owner);
            }
        }

        CardModel? cardToRemove = (await CardSelectCmd.SelectCardsAsync(
            Owner.RunState,
            Owner,
            Owner.Deck.Cards.Where(card => card.IsRemovable),
            minCount: 1,
            maxCount: 1,
            source: this,
            cancelable: false)).FirstOrDefault();
        if (cardToRemove is not null && !inventory.CardRemoval.Purchased)
        {
            await merchant.BuyCardRemoval(cardToRemove, Owner);
        }
    }
}
