using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.MonsterMoves;

/// <summary>怪物招式状态机。逐字移植（<c>MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MonsterMoveStateMachine</c>）。</summary>
public sealed class MonsterMoveStateMachine
{
    private readonly MonsterState _initialState;
    private MonsterState _currentState;
    private bool _performedFirstMove;

    public MonsterMoveStateMachine(IEnumerable<MonsterState> states, MonsterState initialState)
    {
        foreach (MonsterState state in states)
        {
            state.RegisterStates(States);
        }

        _initialState = initialState;
        _currentState = _initialState;
        if (_currentState.ShouldAppearInLogs)
        {
            StateLog.Add(_currentState);
        }
    }

    public Dictionary<string, MonsterState> States { get; } = new();

    public List<MonsterState> StateLog { get; } = new();

    internal MonsterState CurrentState => _currentState;

    internal bool PerformedFirstMove => _performedFirstMove;

    public MoveState RollMove(IEnumerable<Creature> targets, Creature owner, Rng rng)
    {
        FindNextMoveState(owner, rng);
        if (!_currentState.IsMove)
        {
            throw new InvalidOperationException(_currentState.Id + " is not a valid move state");
        }

        return (MoveState)_currentState;
    }

    public void OnMovePerformed(MoveState move)
    {
        _performedFirstMove = true;
    }

    public void ForceCurrentState(MonsterState state) => SetCurrentState(state);

    private void FindNextMoveState(Creature owner, Rng rng)
    {
        if (!_currentState.CanTransitionAway || (!_performedFirstMove && _currentState.IsMove))
        {
            return;
        }

        MonsterState? logged = null;
        do
        {
            string? nextStateId = _currentState.GetNextState(owner, rng);
            if (!string.IsNullOrEmpty(nextStateId) && !States.ContainsKey(nextStateId))
            {
                throw new InvalidOperationException("no valid state found: " + nextStateId);
            }

            SetCurrentState(string.IsNullOrEmpty(nextStateId) ? _initialState : States[nextStateId]);
            logged ??= _currentState.ShouldAppearInLogs ? _currentState : null;
        }
        while (!_currentState.IsMove);

        if (logged != null)
        {
            StateLog.Add(logged);
        }
    }

    private void SetCurrentState(MonsterState state)
    {
        _currentState.OnExitState();
        _currentState = state;
        _currentState.OnEnterState();
    }

    internal MoveState? RestoreRuntimeStateFrom(
        MonsterMoveStateMachine source,
        MoveState? sourceNextMove,
        MonsterModel owner)
    {
        _performedFirstMove = source._performedFirstMove;
        var stateMap = new Dictionary<MonsterState, MonsterState>(ReferenceEqualityComparer.Instance);
        foreach ((string id, MonsterState sourceState) in source.States)
        {
            MonsterState clonedState = States[id];
            stateMap.Add(sourceState, clonedState);
            if (sourceState is MoveState sourceMove && clonedState is MoveState clonedMove)
            {
                clonedMove.RestoreRuntimeStateFrom(sourceMove);
            }
        }

        MonsterState RestoreState(MonsterState sourceState)
        {
            if (stateMap.TryGetValue(sourceState, out MonsterState? clonedState))
            {
                return clonedState;
            }

            if (sourceState is not MoveState sourceMove)
            {
                throw new InvalidOperationException(
                    $"Transient monster state {sourceState.Id} is not a move and cannot be cloned.");
            }

            MoveState clonedMove = sourceMove.CloneTransientForCombat(owner);
            clonedMove.RestoreRuntimeStateFrom(sourceMove);
            stateMap.Add(sourceState, clonedMove);
            return clonedMove;
        }

        _currentState = RestoreState(source._currentState);
        StateLog.Clear();
        foreach (MonsterState state in source.StateLog)
        {
            StateLog.Add(RestoreState(state));
        }

        return sourceNextMove is null
            ? null
            : (MoveState)RestoreState(sourceNextMove);
    }
}
