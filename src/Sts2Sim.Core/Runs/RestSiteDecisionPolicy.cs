namespace Sts2Sim.Core.Runs;

/// <summary>Shared default selection and membership validation for a rest-site snapshot.</summary>
public static class RestSiteDecisionPolicy
{
    public static RestSiteDecision ChooseDefault(IReadOnlyList<RestSiteDecision> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.Where(candidate => candidate.IsEnabled).OrderBy(candidate => candidate.Priority).FirstOrDefault()
            ?? throw new InvalidOperationException("The rest-site candidate snapshot is empty.");
    }

    public static bool Contains(
        IReadOnlyList<RestSiteDecision> candidates,
        RestSiteDecision decision)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(decision);
        return decision.IsEnabled && candidates.Any(candidate => candidate.IsEnabled && candidate == decision);
    }
}
