using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TheCourier : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsAllowedInShops => false;

    public override decimal ModifyMerchantPrice(
        Player player,
        MerchantEntry entry,
        decimal originalPrice) =>
        player == Owner ? originalPrice * 0.8m : originalPrice;

    public override bool ShouldRefillMerchantEntry(MerchantEntry entry, Player player) =>
        player == Owner;
}
