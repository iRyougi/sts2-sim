using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Combat.StateDescription;
using System.Text.Json;

namespace Sts2Sim.Core.Combat;

/// <summary>Optional decision-source notification before any combat-start RNG scope opens.</summary>
public interface ICombatOpeningPlanner
{
    bool RequiresOpeningSnapshot => true;
    Task PrepareOpeningAsync(CombatOpeningContext context);
}

/// <summary>Read-only observation of actions in an independent opening replay.</summary>
public interface ICombatOpeningObserver
{
    void OpeningStarted(CombatState state);
    void CardStarted(CombatState state, CardModel card);
    void CardFinished(CombatState state, CardModel card);
    void PotionStarted(CombatState state, PotionModel potion);
    void PotionFinished(CombatState state, PotionModel potion);
}

/// <summary>
/// Owns an exact pre-start run snapshot. Replays start the already prepared room,
/// without repeating its identity, before-entry hooks, encounter creation or diagnostics.
/// </summary>
public sealed class CombatOpeningContext
{
    /// <summary>Exact boundary plus keyed draw history; no display text or hidden data leaves combat.</summary>
    public static string Fingerprint(CombatState state)
    {
        var builder = new CombatStateDescriptionBuilder();
        CombatStateDescription.AppendExactState(ref builder, state);
        return JsonSerializer.Serialize(new
        {
            State = builder.Build(),
            Run = state.RunState.Rng.GetKeyedDraws(),
            Players = state.Players.Select(p => p.PlayerRng.GetKeyedDraws()).ToArray(),
        });
    }

    private readonly RunState _root;
    private readonly bool _afterRoomEntered;

    internal CombatOpeningContext(RunState run, CombatRoom room, bool afterRoomEntered)
    {
        if (!ReferenceEquals(run.CurrentRoom, room))
            throw new InvalidOperationException("Opening snapshots require the current combat room.");
        // CloneExact enforces that no RNG scope, active combat or event callback is open.
        _root = run.CloneExact();
        _afterRoomEntered = afterRoomEntered;
    }

    /// <summary>Executes all of StartCombatAsync on a fresh independently owned run graph.</summary>
    public Task<CombatState> ReplayAsync(ICardSelectionDecisionSource selection,
        CancellationToken cancellationToken = default) => ReplayObservedAsync(selection, null, cancellationToken);

    /// <summary>Returns an independent pre-start state for common policy baseline capture.</summary>
    public CombatState CopyPreparedState()
    {
        RunState run = _root.CloneExact();
        return ((CombatRoom)run.CurrentRoom!).PreparedOpeningState;
    }

    public async Task<CombatState> ReplayObservedAsync(ICardSelectionDecisionSource selection,
        ICombatOpeningObserver? observer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        cancellationToken.ThrowIfCancellationRequested();
        RunState run = _root.CloneExact();
        var room = run.CurrentRoom as CombatRoom
            ?? throw new InvalidOperationException("Opening snapshot lost its combat room.");
        run.ConfigureCardSelectionSource(selection);
        await room.ReplayPreparedOpeningAsync(run, selection, _afterRoomEntered, observer);
        cancellationToken.ThrowIfCancellationRequested();
        return room.Engine.State;
    }
}

internal sealed class CombatOpeningObserverAdapter(CombatState state, ICombatOpeningObserver observer)
    : ICombatObserver
{
    public void CombatStarted(CombatState _) { }
    public void PlayerTurnStarted(CombatState _) { }
    public void PlayerTurnEnded(CombatState _) { }
    public void CardDrawn(CardModel card) { }
    public void CardPlayStarted(CardModel card, Creature? target) => observer.CardStarted(state, card);
    public void CardPlayFinished(CardModel card, Creature? target, CardPlay? play) => observer.CardFinished(state, card);
    public void EnemyMoveStarted(Creature source, string moveId) { }
    public void EnemyMoveFinished(Creature source, string moveId) { }
    public void PotionUseStarted(PotionModel potion, Creature? target) => observer.PotionStarted(state, potion);
    public void PotionUseFinished(PotionModel potion, Creature? target) => observer.PotionFinished(state, potion);
}
