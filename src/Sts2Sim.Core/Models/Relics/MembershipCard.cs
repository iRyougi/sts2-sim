using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MembershipCard : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override decimal ModifyMerchantPrice(
        Player player,
        MerchantEntry entry,
        decimal originalPrice) =>
        player == Owner ? originalPrice * 0.5m : originalPrice;
}
