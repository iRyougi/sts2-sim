using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class PerfectFit : EnchantmentModel
{
    public override void ModifyShuffleOrder(Player player, List<CardModel> cards, bool isInitialShuffle)
    {
        if (isInitialShuffle || player != Owner.Owner || !cards.Remove(Owner)) return;
        cards.Insert(0, Owner);
    }
}
