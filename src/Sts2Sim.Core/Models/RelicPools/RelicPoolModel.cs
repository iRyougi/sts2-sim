namespace Sts2Sim.Core.Models.RelicPools;

/// <summary>Stable character-relic membership used when constructing reward grab bags.</summary>
public abstract class RelicPoolModel
{
    public abstract IReadOnlyList<RelicModel> AllRelics { get; }
}

internal sealed class EmptyRelicPool : RelicPoolModel
{
    public static EmptyRelicPool Instance { get; } = new();

    public override IReadOnlyList<RelicModel> AllRelics => Array.Empty<RelicModel>();
}
