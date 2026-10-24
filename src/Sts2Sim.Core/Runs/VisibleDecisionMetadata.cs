using System.Collections.Immutable;
using System.Globalization;

namespace Sts2Sim.Core.Runs;

/// <summary>Localization-independent values already visible at a decision boundary.</summary>
public sealed record VisibleDecisionMetadata(
    ImmutableArray<VisibleValue> ContextValues,
    ImmutableArray<VisibleOption> DisplayOptions)
{
    public ImmutableArray<string> CoverageGapIds { get; init; } = [];
    public static VisibleDecisionMetadata Empty { get; } = new([], []);
}

public sealed record VisibleOption(string OptionKey, ImmutableArray<VisibleValue> Values)
{
    public bool IsEnabled { get; init; } = true;
    public ImmutableArray<VisibleContent> Contents { get; init; } = [];
    public ImmutableArray<string> CoverageGapIds { get; init; } = [];
}

public sealed record VisibleContent(
    string ContentId,
    ImmutableArray<VisibleValue> Values,
    ImmutableArray<VisibleContent> Children)
{
    public ImmutableArray<string> CoverageGapIds { get; init; } = [];
}

public sealed record VisibleValue(string Name, VisibleValueKind Kind, string Value)
{
    public static VisibleValue Int(string name, int value) =>
        new(name, VisibleValueKind.Integer, value.ToString(CultureInfo.InvariantCulture));

    public static VisibleValue Number(string name, decimal value) =>
        new(name, VisibleValueKind.DisplayDecimal, value.ToString(CultureInfo.InvariantCulture));

    public static VisibleValue Id(string name, string value) => new(name, VisibleValueKind.ContentId, value);
    public static VisibleValue Key(string name, string value) => new(name, VisibleValueKind.LocalizationKey, value);
    public static VisibleValue Scope(string name, string value) => new(name, VisibleValueKind.Scope, value);
    public static VisibleValue Condition(string name, string value) => new(name, VisibleValueKind.Condition, value);
}

public enum VisibleValueKind
{
    Integer,
    DisplayDecimal,
    ContentId,
    LocalizationKey,
    Scope,
    Condition,
}

/// <summary>Binds visible rows to the original offered objects without exporting their identity.</summary>
public sealed class VisibleDecisionSnapshot
{
    private readonly Dictionary<object, VisibleOption> _candidateDescriptions =
        new(ReferenceEqualityComparer.Instance);

    public VisibleDecisionMetadata Metadata { get; }

    public VisibleDecisionSnapshot(VisibleDecisionMetadata metadata, IReadOnlyList<object?> displayCandidates)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(displayCandidates);
        if (metadata.DisplayOptions.Length != displayCandidates.Count)
            throw new ArgumentException("Every displayed candidate must have exactly one visible row.", nameof(displayCandidates));

        Metadata = metadata;
        for (int i = 0; i < displayCandidates.Count; i++)
        {
            if (displayCandidates[i] is not { } candidate) continue;
            if (!_candidateDescriptions.TryAdd(candidate, metadata.DisplayOptions[i]))
                throw new ArgumentException("A displayed candidate object must occur only once.", nameof(displayCandidates));
        }
    }

    public VisibleOption DescribeCandidate(object candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return _candidateDescriptions.TryGetValue(candidate, out VisibleOption? description)
            ? description
            : throw new ArgumentException("Candidate is not an original object offered by this snapshot.", nameof(candidate));
    }
}