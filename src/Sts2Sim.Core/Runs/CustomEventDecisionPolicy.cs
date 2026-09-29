using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;

namespace Sts2Sim.Core.Runs;

public static class CustomEventDecisionPolicy
{
    /// <summary>Uses only the revealed board state; hidden item identities are not policy inputs.</summary>
    public static CustomEventDecision ChooseDefault(EventModel @event)
    {
        if (@event is FakeMerchant) return new CustomEventDecision.Leave();
        if (@event is CrystalSphere { Game: { } game })
        {
            for (int x = 0; x < game.Width; x++)
                for (int y = 0; y < game.Height; y++)
                    if (game.IsHidden(x, y)) return new CustomEventDecision.RevealSphere(x, y);
            return new CustomEventDecision.RevealSphere(0, 0);
        }
        throw new InvalidOperationException("No custom interaction is available for this event.");
    }

    internal static Task ExecuteAsync(EventModel @event, CustomEventDecision decision)
    {
        if (@event.IsFinished || @event.IsAwaitingForcedCombat || @event.CurrentOptions.Count != 0)
            throw new InvalidOperationException("This event cannot accept a custom action now.");
        return (@event, decision) switch
        {
            (CrystalSphere sphere, CustomEventDecision.RevealSphere reveal) =>
                sphere.RevealAsync(reveal.X, reveal.Y, reveal.Big),
            (FakeMerchant merchant, CustomEventDecision.BuyRelic buy) => merchant.Buy(buy.Entry),
            (FakeMerchant merchant, CustomEventDecision.Leave) => merchant.Leave(),
            (FakeMerchant merchant, CustomEventDecision.UsePotion use) =>
                PotionCmd.Use(use.Potion, merchant.Owner, target: null),
            _ => throw new InvalidOperationException("Custom action does not belong to the current event."),
        };
    }
}
