using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.MonsterMoves;

/// <summary>一个具体的怪物招式。逐字移植（<c>MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState</c>）。</summary>
public sealed class MoveState : MonsterState
{
    private readonly Func<IReadOnlyList<Creature>, Task> _onPerform;
    private bool _performedAtLeastOnce;

    public MoveState(string stateId, Func<IReadOnlyList<Creature>, Task> onPerform, params AbstractIntent[] intents)
    {
        StateId = stateId;
        _onPerform = onPerform;
        Intents = intents;
    }

    public IReadOnlyList<AbstractIntent> Intents { get; }

    public string StateId { get; }

    public bool MustPerformOnceBeforeTransitioning { get; init; }

    public string? FollowUpStateId { get; init; }

    public MonsterState? FollowUpState { get; set; }

    internal Func<MonsterModel, MoveState>? CombatCloneFactory { get; init; }

    public override bool CanTransitionAway => MustPerformOnceBeforeTransitioning ? _performedAtLeastOnce : true;

    public override bool IsMove => true;

    public override string Id => StateId;

    public async Task PerformMove(IEnumerable<Creature> targets)
    {
        _performedAtLeastOnce = true;
        await _onPerform(targets as Creature[] ?? targets.ToArray());
    }

    public override void OnExitState()
    {
        _performedAtLeastOnce = false;
    }

    public override string GetNextState(Creature owner, Rng rng)
    {
        return (FollowUpState?.Id ?? FollowUpStateId) ?? throw new InvalidOperationException("No valid followup state.");
    }

    public override void RegisterStates(Dictionary<string, MonsterState> monsterStates)
    {
        monsterStates.Add(Id, this);
    }

    internal MoveState CloneTransientForCombat(MonsterModel owner)
    {
        if (CombatCloneFactory is null)
        {
            throw new InvalidOperationException($"Transient move {StateId} does not support combat cloning.");
        }

        return CombatCloneFactory(owner);
    }

    internal void RestoreRuntimeStateFrom(MoveState source)
    {
        _performedAtLeastOnce = source._performedAtLeastOnce;
    }
}
