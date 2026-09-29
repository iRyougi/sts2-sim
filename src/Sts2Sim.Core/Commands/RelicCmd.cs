using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Commands;

/// <summary>Relic obtain commands.</summary>
public static class RelicCmd
{
    /// <summary>Obtains a relic, stacking an existing stackable copy when present.</summary>
    public static async Task Obtain(RelicModel canonical, Player player)
    {
        string semanticKey = $"{player.CurrentSemanticLocationKey}/relic={canonical.Id.Entry}/operation=obtain";
        using IDisposable runRngScope = player.RunState.Rng.BeginSemanticScope(semanticKey);
        using IDisposable playerRngScope = player.PlayerRng.BeginSemanticScope(semanticKey);
        RelicModel? existing = canonical.IsStackable
            ? player.Relics.FirstOrDefault(r => r.GetType() == canonical.GetType())
            : null;
        if (existing != null)
        {
            existing.IncrementStackCount();
            return;
        }

        var relic = (RelicModel)canonical.MutableClone();
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
        RemoveObtainedRelicFromBags(player, relic);
        await relic.AfterObtained();
    }

    private static void RemoveObtainedRelicFromBags(Player player, RelicModel relic)
    {
        if (!relic.IsStackable)
        {
            player.RelicGrabBag.Remove(relic);
            RelicFactory.RemoveFromSharedBag(player, relic);
        }
    }

    public static async Task Replace(RelicModel oldRelic, RelicModel newRelic)
    {
        ArgumentNullException.ThrowIfNull(oldRelic);
        ArgumentNullException.ThrowIfNull(newRelic);
        Player owner = oldRelic.Owner;
        if (owner is null || !owner.Relics.Contains(oldRelic))
        {
            throw new InvalidOperationException("The old relic is not owned by a player.");
        }
        if (newRelic.Owner is not null && !ReferenceEquals(newRelic.Owner, owner))
        {
            throw new InvalidOperationException("The replacement relic belongs to a different player.");
        }
        if (owner.Relics.Contains(newRelic))
        {
            throw new InvalidOperationException("The replacement relic is already in the player inventory.");
        }

        string semanticKey = $"{owner.CurrentSemanticLocationKey}/relic={newRelic.Id.Entry}/operation=replace";
        using IDisposable runRngScope = owner.RunState.Rng.BeginSemanticScope(semanticKey);
        using IDisposable playerRngScope = owner.PlayerRng.BeginSemanticScope(semanticKey);

        RelicModel replacement = newRelic.IsCanonical
            ? (RelicModel)newRelic.MutableClone()
            : newRelic;
        int index = owner.RemoveRelicInternal(oldRelic);
        await oldRelic.AfterRemoved();
        if (!ReferenceEquals(replacement.Owner, owner))
        {
            replacement.AssignOwner(owner);
        }
        owner.InsertRelicInternal(index, replacement);
        RemoveObtainedRelicFromBags(owner, replacement);
        await replacement.AfterObtained();
    }
}
