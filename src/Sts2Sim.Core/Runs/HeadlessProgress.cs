namespace Sts2Sim.Core.Runs;

/// <summary>Immutable caller-owned cross-run progress. Export a run's value for the next run;
/// constructing or searching a run never mutates a shared save or global singleton.</summary>
public sealed record HeadlessProgress
{
    public int WongoPoints { get; }

    public HeadlessProgress(int wongoPoints = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wongoPoints);
        WongoPoints = wongoPoints;
    }
}
