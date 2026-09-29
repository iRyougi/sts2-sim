namespace Sts2Sim.Core.Reporting;

/// <summary>
/// Reporting schema versions emitted by the built-in writer. Version 1.2.0 adds an explicit
/// combat-log index while preserving the additive fields introduced in 1.1.0.
/// </summary>
public static class ReportingSchema
{
    public const string CurrentVersion = "1.2.0";
}
