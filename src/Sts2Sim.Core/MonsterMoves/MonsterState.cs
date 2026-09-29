using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.MonsterMoves;

/// <summary>怪物 AI 状态机节点基类。逐字移植（<c>MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MonsterState</c>）。</summary>
public abstract class MonsterState
{
    public abstract string Id { get; }

    public virtual bool ShouldAppearInLogs => true;

    public virtual bool CanTransitionAway => true;

    public virtual bool IsMove => this is MoveState;

    public abstract string? GetNextState(Creature owner, Rng rng);

    public abstract void RegisterStates(Dictionary<string, MonsterState> monsterStates);

    public virtual void OnEnterState()
    {
    }

    public virtual void OnExitState()
    {
    }
}
