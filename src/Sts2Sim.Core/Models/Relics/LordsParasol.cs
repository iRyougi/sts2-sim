using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LordsParasol : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // #1047：原版不改商店价格（没有 ModifyMerchantPrice），只在进店时以 ignoreCost: true 依次买下角色卡、无色卡、
    // 遗物、药水，再做一次不可取消的免费删牌；买不下的药水（栏位满、Sozu）留在货架上并保持原价。
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
                await merchant.Buy(entry, Owner, ignoreCost: true);
            }
        }
        foreach (MerchantRelicEntry entry in inventory.Relics.ToArray())
        {
            if (!entry.Purchased)
            {
                await merchant.Buy(entry, Owner, ignoreCost: true);
            }
        }
        foreach (MerchantPotionEntry entry in inventory.Potions.ToArray())
        {
            if (!entry.Purchased)
            {
                await merchant.Buy(entry, Owner, ignoreCost: true);
            }
        }

        // 原版 OnTryPurchaseUnsynchronized 在 IsStocked 为 false（删牌已用）时直接返回，不打开选牌。
        if (inventory.CardRemoval.Purchased)
        {
            return;
        }

        // 原版 MerchantCardRemovalEntry.DoCardRemoval → CardSelectCmd.FromDeckForRemoval：诅咒排在最前，其余按牌组顺序。
        CardModel? cardToRemove = (await CardSelectCmd.FromDeckForRemoval(Owner, 1, this)).FirstOrDefault();
        if (cardToRemove is not null)
        {
            await merchant.BuyCardRemoval(cardToRemove, Owner, ignoreCost: true);
        }
    }
}
