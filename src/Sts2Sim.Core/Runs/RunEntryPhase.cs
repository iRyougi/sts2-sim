namespace Sts2Sim.Core.Runs;

/// <summary>Engine control state; never a strategic model feature.</summary>
public enum RunEntryPhase { Legacy, InitializedBeforeAncient, AncientInProgress, ReadyForMap }

public sealed record ActEntryContinuation(bool ReachedBoundary, RunDriver.Result? Terminal);