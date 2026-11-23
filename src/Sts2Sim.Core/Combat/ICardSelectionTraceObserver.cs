using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>Read-only observation of card-selection requests and their validated results.
/// Observers must not choose cards, mutate game state, or consume RNG. Exceptions propagate to
/// the caller; a failed selection has no successful result notification.</summary>
public interface ICardSelectionTraceObserver
{
    void BeforeSelection(CardSelectionRequest request);

    /// <summary>For bundle selection, selected contains the entire selected bundle.</summary>
    void AfterSelection(CardSelectionRequest request, IReadOnlyList<CardModel> selected, bool automatic);
}
