using Sts2Sim.Core.Content;
using Sts2Sim.Core.Events;

namespace Sts2Sim.Core.Runs;

/// <summary>Shared default selection from the current event's legal options.</summary>
public static class EventOptionDecisionPolicy
{
    public static EventOption ChooseDefault(IReadOnlyList<EventOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EventOption? fallback = null;
        foreach (EventOption option in options)
        {
            if (option.IsLocked) continue;
            fallback ??= option;
            // Apply content preferences only at decision time, without changing event generation.
            if (!CrossCharacterContentExclusions.IsExcludedKey(option.Key)) return option;
        }
        return fallback ?? throw new InvalidOperationException("No unlocked event options are available.");
    }
}
