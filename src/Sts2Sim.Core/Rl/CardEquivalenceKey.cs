using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Rl;

/// <summary>
/// Deviation #188: the deck backing removal/smith candidates is unbounded, so the RL action space
/// addresses decision-equivalence classes instead of raw deck instances. Two cards are the same
/// choice when they share a type, an upgrade level, and an enchantment (including magnitude) —
/// enchantments are part of the key because an enchanted duplicate (e.g. a <c>Glam</c> Strike) is a
/// materially different choice from a plain one, not the same card twice.
/// </summary>
internal static class CardEquivalenceKey
{
    public static string For(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        string enchantments = string.Join(
            ",",
            card.Enchantments
                .Select(enchantment => $"{enchantment.GetType().Name}:{enchantment.Magnitude}")
                .OrderBy(key => key, StringComparer.Ordinal));
        return $"{card.GetType().Name}#{card.CurrentUpgradeLevel}#{enchantments}";
    }
}
