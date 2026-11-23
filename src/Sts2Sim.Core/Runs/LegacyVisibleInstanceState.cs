namespace Sts2Sim.Core.Runs;

/// <summary>Frozen schema-2 payloads. New consumers use VisibleContent instead.</summary>
public sealed record LegacyVisibleCard(string Id, int UpgradeLevel, IReadOnlyList<string> Enchantments)
{
    public IReadOnlyList<LegacyVisibleEnchantment>? EnchantmentInstances { get; init; }
    public IReadOnlyList<LegacyVisibleNumericDelta>? NumericDeltas { get; init; }
    public IReadOnlyList<LegacyVisibleCategory>? InstanceCategories { get; init; }
}
public sealed record LegacyVisibleRelic(string Id)
{
    public bool? CounterVisible { get; init; }
    public double? CounterAmount { get; init; }
    public IReadOnlyList<string>? VisibleStates { get; init; }
}
public sealed record LegacyVisibleEnchantment(string Id, double? Amount, string Status);
public sealed record LegacyVisibleNumericDelta(string Name, double Delta);
public sealed record LegacyVisibleCategory(string Name, string Value);
