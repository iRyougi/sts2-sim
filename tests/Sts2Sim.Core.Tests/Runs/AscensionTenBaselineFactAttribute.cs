namespace Sts2Sim.Core.Tests.Runs;

/// <summary>Opt-in gate for the one-off A10 neutral-baseline probe.</summary>
internal sealed class AscensionTenBaselineFactAttribute : FactAttribute
{
    public AscensionTenBaselineFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("STS2_A10_BASELINE"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set STS2_A10_BASELINE=1 to run the A10 neutral-baseline probe.";
        }
    }
}
