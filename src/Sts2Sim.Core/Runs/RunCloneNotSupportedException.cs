namespace Sts2Sim.Core.Runs;

public enum RunCloneRejectionReason
{
    ActiveCombat,
    PendingCallback,
    PendingTransplant,
    OpenSemanticScope,
    MissingVisibleUnknownHistory,
    MissingVisibleAncientHistory,
}

/// <summary>A run snapshot was requested outside an approved stable decision boundary.</summary>
public class RunCloneNotSupportedException : InvalidOperationException
{
    public RunCloneRejectionReason Reason { get; }

    public RunCloneNotSupportedException(RunCloneRejectionReason reason, string detail)
        : base($"Run cloning rejected ({reason}): {detail}")
    {
        Reason = reason;
    }
}

/// <summary>Only an act with previously entered Unknown rooms and incomplete public records is rejected.</summary>
public sealed class MissingVisibleUnknownHistoryException : RunCloneNotSupportedException
{
    public MissingVisibleUnknownHistoryException()
        : base(RunCloneRejectionReason.MissingVisibleUnknownHistory,
            "This act has entered Unknown rooms without complete visible result/rule records.")
    {
    }
}
