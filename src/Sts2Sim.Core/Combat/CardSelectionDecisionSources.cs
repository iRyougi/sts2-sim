namespace Sts2Sim.Core.Combat;

/// <summary>Existing Core card-selection policies exposed through their runtime interface.</summary>
public static class CardSelectionDecisionSources
{
    public static ICardSelectionDecisionSource RunEngine => RunEngineCardSelectionDecisionSource.Instance;

    public static ICardSelectionDecisionSource Rejecting => RejectingCardSelectionDecisionSource.Instance;
}
