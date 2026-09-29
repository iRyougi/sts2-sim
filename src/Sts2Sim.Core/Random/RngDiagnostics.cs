namespace Sts2Sim.Core.Random;

/// <summary>
/// Parameters passed to the RNG draw observer: instance, raw method name, lower bound,
/// upper bound, and parameter count (0, 1, 2, or -1 when parameters cannot be compared).
/// Double values avoid boxing allocations.
/// </summary>
public delegate void RngDrawObserver(Rng rng, string op, double min, double max, int arity);

/// <summary>
/// Diagnostic-only hook invoked once after each raw draw that advances an RNG counter.
/// Production and training paths must leave this null. A null observer adds only a field
/// read and null check, without allocating or changing the draw.
/// </summary>
public static class RngDiagnostics
{
    public static RngDrawObserver? DrawObserver;
}
