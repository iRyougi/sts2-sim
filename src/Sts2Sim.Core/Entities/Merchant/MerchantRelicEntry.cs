using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Entities.Merchant;

public sealed class MerchantRelicEntry : MerchantEntry
{
    public RelicModel Relic { get; }

    public MerchantRelicEntry(RelicModel relic, int price, Player? player = null)
        : base(price, player)
    {
        Relic = relic;
    }
}
