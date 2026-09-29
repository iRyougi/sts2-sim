namespace Sts2Sim.Core.Tests.Runs;

/// <summary>Opt-in gate for the unkillable-run invariant probe.</summary>
internal sealed class RunInvariantProbeFactAttribute : FactAttribute
{
    public RunInvariantProbeFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("STS2_INVARIANT"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set STS2_INVARIANT=1 to run the unkillable-run invariant probe.";
        }
    }
}
