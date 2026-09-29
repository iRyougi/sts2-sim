using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rl;

/// <summary>
/// Labels the immutable rest-site snapshot shared by observation encoding and action decoding.
/// Deviation #188: the deck backing Smith candidates is unbounded (a <c>BingBong</c> deck can
/// carry thousands of instances), so duplicate cards that share a type, upgrade level, and
/// enchantment (see <see cref="CardEquivalenceKey"/>) are collapsed into one decision-equivalence
/// class — the RL agent chooses "upgrade a Strike," not "upgrade copy #3717." Each class exposes
/// its first deck instance as the representative, since <see cref="Rooms.RestSiteRoom"/> and
/// <see cref="RestSiteDecision.Smith"/> need the exact persistent-deck card reference to resolve.
/// </summary>
internal static class RestSiteDecisionCandidates
{
    internal sealed record Candidate(RestSiteDecision Decision, string Label, int SlotIndex);

    public static IReadOnlyList<Candidate> Build(
        Player player,
        IReadOnlyList<RestSiteDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(decisions);

        int distinctSmithClasses = decisions
            .Where(decision => decision.IsEnabled)
            .OfType<RestSiteDecision.Smith>()
            .Select(smith => CardEquivalenceKey.For(smith.Card))
            .Distinct(StringComparer.Ordinal)
            .Count();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            distinctSmithClasses,
            ActionSpaceLayout.MaxDeckBackedChoices);

        var seenSmithClasses = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<Candidate>(decisions.Count);
        int legacyPosition = 0;
        foreach (RestSiteDecision decision in decisions)
        {
            if (!decision.IsEnabled)
            {
                continue;
            }

            if (decision is RestSiteDecision.Cook)
            {
                // Preserve Cook's existing wire slot; future options use the generic packed slots.
                candidates.Add(new Candidate(decision, decision.GetLabel(player), ActionSpaceLayout.CookRestSiteIndex));
                continue;
            }

            if (decision is RestSiteDecision.Smith smithDuplicateCheck &&
                !seenSmithClasses.Add(CardEquivalenceKey.For(smithDuplicateCheck.Card)))
            {
                // A decision-equivalent card (same type/upgrade/enchantment) already claimed the
                // representative slot for this class; later duplicates are folded in rather than
                // exposed as separate actions.
                continue;
            }

            if (legacyPosition >= ActionSpaceLayout.MaxRestSiteChoices + ActionSpaceLayout.MaxRestSiteOverflowChoices)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(decisions),
                    "Rest-site candidates exceed the fixed action capacity.");
            }

            candidates.Add(new Candidate(
                decision,
                decision.GetLabel(player),
                ActionSpaceLayout.PackedRestSiteIndex(legacyPosition++)));
        }

        return candidates;
    }
}
