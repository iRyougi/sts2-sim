using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Entities.Merchant;

public sealed class MerchantPotionEntry : MerchantEntry
{
    public PotionModel Potion { get; }

    public MerchantPotionEntry(PotionModel potion, int price, Player? player = null)
        : base(price, player)
    {
        Potion = potion;
    }
}
