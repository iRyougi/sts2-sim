namespace Sts2Sim.Core.Combat;

/// <summary>A combat decision paused while its caller supplies another choice.</summary>
public abstract class DecisionSuspendedException : Exception
{
    protected DecisionSuspendedException(string message) : base(message) { }
}
