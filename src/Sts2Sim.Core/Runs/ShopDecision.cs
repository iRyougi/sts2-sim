using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Runs;

/// <summary>A single pull-based shop decision. Decisions repeat until <see cref="Leave"/>.</summary>
public abstract record ShopDecision
{
    public sealed record BuyCard(MerchantCardEntry Entry) : ShopDecision;

    public sealed record BuyRelic(MerchantRelicEntry Entry) : ShopDecision;

    public sealed record BuyPotion(MerchantPotionEntry Entry) : ShopDecision;

    public sealed record BuyCardRemoval(CardModel CardToRemove) : ShopDecision;

    public sealed record Leave : ShopDecision;
}
