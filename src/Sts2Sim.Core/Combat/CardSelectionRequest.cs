using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>A bounded card choice exposed to run, combat, and search decision sources.</summary>
public sealed record CardSelectionRequest(
    Player Player,
    IReadOnlyList<CardModel> Candidates,
    int MinCount,
    int MaxCount,
    AbstractModel? Source,
    bool Cancelable = false,
    IReadOnlyList<IReadOnlyList<CardModel>>? Bundles = null)
{
    /// <summary>Prepared event layouts and completed combats are not live combat decisions.
    /// Startup hooks run before IsInProgress, so the engine's setup window counts too.</summary>
    public bool IsLiveCombatSelection => Player.Creature.CombatState is CombatState
        { IsProjection: false, Engine: { IsInProgress: true } or { IsStarting: true } };

    /// <summary>For atomic bundle choices Candidates contains one identity per bundle; Bundles exposes
    /// all visible cards in matching order. Returning a candidate selects that entire bundle.</summary>
    public bool IsBundleSelection => Bundles is not null;
    public bool CanCancel => Cancelable;
}

public interface ICardSelectionDecisionSource
{
    Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request);
}

/// <summary>Optional synchronous observation of an automatic result before the caller applies its effect.
/// Observers must not choose cards, mutate card state, or consume RNG.</summary>
public interface IAutomaticCardSelectionObserver
{
    void ObserveAutomaticSelection(CardSelectionRequest request, IReadOnlyList<CardModel> selected);
}

internal sealed class RejectingCardSelectionDecisionSource : ICardSelectionDecisionSource
{
    public static RejectingCardSelectionDecisionSource Instance { get; } = new();

    private RejectingCardSelectionDecisionSource()
    {
    }

    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
        throw new InvalidOperationException(
            "A non-forced combat card selection requires a configured decision source.");
}

/// <summary>
/// Explicit deterministic card-selection policy for the autonomous <see cref="Runs.RunEngine"/>.
/// Combat states still fail closed until a production driver installs a decision source.
/// </summary>
internal sealed class RunEngineCardSelectionDecisionSource : ICardSelectionDecisionSource
{
    public static RunEngineCardSelectionDecisionSource Instance { get; } = new();

    private RunEngineCardSelectionDecisionSource()
    {
    }

    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<CardModel> selected = request.Candidates
            .Skip(Math.Max(0, request.Candidates.Count - request.MaxCount))
            .ToArray();
        return Task.FromResult(selected);
    }
}
