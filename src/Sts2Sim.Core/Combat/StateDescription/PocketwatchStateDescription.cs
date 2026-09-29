namespace Sts2Sim.Core.Combat.StateDescription;

public readonly record struct PocketwatchStateDescription(
    int CardsPlayedThisTurn,
    int CardsPlayedPreviousTurn,
    bool ShouldDrawExtra,
    bool CanStillTriggerThisTurn,
    int CardPlayThresholdSnapshot);
