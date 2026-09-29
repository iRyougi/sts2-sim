using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Runs;

/// <summary>Headless actions for events without ordinary option buttons (deviation #284).</summary>
public abstract record CustomEventDecision
{
    public sealed record RevealSphere(int X, int Y, bool Big = true) : CustomEventDecision;
    public sealed record BuyRelic(MerchantRelicEntry Entry) : CustomEventDecision;
    public sealed record UsePotion(PotionModel Potion) : CustomEventDecision;
    public sealed record Leave : CustomEventDecision;
}
