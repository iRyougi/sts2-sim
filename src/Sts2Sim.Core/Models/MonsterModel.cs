namespace Sts2Sim.Core.Models;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Combat;

/// <summary>
/// 怪物基类。逐字移植（<c>MegaCrit.Sts2.Core.Models.MonsterModel</c>）。
/// 偏离 #36 已销案（2026-09-07 复核）：<c>SetUniqueMonsterHpValue</c>（HP 区间内随机滚动、
/// 同侧同类不重复）已实现于 <see cref="Creature"/>，并由 <c>CombatState</c> 在生产路径上以
/// <c>Rng.Niche</c> 调用；<c>MonsterHpRollingTests</c> 锁定该行为。
/// </summary>
public abstract class MonsterModel : AbstractModel, ICombatStateDescriptionContributor
{
    public abstract int MinInitialHp { get; }

    public abstract int MaxInitialHp { get; }

    public virtual bool IsHealthBarVisible => true;

    public Rng Rng { get; set; } = null!;

    public RunRngSet RunRng { get; set; } = null!;

    public Creature Creature { get; private set; } = null!;

    public bool IsPerformingMove { get; private set; }

    public MonsterMoveStateMachine? MoveStateMachine { get; private set; }

    public MoveState? NextMove { get; private set; }

    public bool SpawnedThisTurn { get; private set; }

    public override bool ShouldReceiveCombatHooks => true;

    protected abstract MonsterMoveStateMachine GenerateMoveStateMachine();

    public void AssignCreature(Creature creature)
    {
        AssertMutable();
        Creature = creature;
    }

    public void SetUpForCombat()
    {
        AssertMutable();
        MoveStateMachine = GenerateMoveStateMachine();
        SpawnedThisTurn = true;
    }

    public virtual Task AfterAddedToRoom() => Task.CompletedTask;

    public void RollMove(IEnumerable<Creature> targets)
    {
        AssertMutable();
        Rng moveRng = Creature.CombatState is CombatState combatState
            ? combatState.MonsterAiRng(Creature)
            : RunRng.MonsterAi;
        NextMove = MoveStateMachine!.RollMove(targets, Creature, moveRng);
    }

    public void SetMoveImmediate(MoveState state, bool forceTransition = false)
    {
        AssertMutable();
        if (NextMove is null || NextMove.CanTransitionAway || forceTransition)
        {
            NextMove = state;
            MoveStateMachine!.ForceCurrentState(state);
        }
    }

    public async Task PerformMove()
    {
        AssertMutable();
        MoveState move = NextMove ?? throw new InvalidOperationException("No move has been rolled.");
        IReadOnlyList<Creature> targets = Creature.CombatState!.PlayerCreatures;
        using IDisposable? rngScope = (Creature.CombatState as CombatState)?.BeginMonsterRngScope(Creature);
        IsPerformingMove = true;
        try
        {
            await move.PerformMove(targets);
            MoveStateMachine!.OnMovePerformed(move);
        }
        finally
        {
            IsPerformingMove = false;
        }

        if (Creature.CombatState is { } combatState && Creature.IsDead &&
            Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, Creature))
        {
            combatState.RemoveCreature(Creature);
        }
    }

    public void OnSideSwitch()
    {
        AssertMutable();
        SpawnedThisTurn = false;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        Creature = null!;
        Rng = null!;
        RunRng = null!;
        IsPerformingMove = false;
        MoveStateMachine = null;
        NextMove = null;
        SpawnedThisTurn = false;
    }

    internal void PrepareCombatCloneStateFrom(
        MonsterModel source,
        Creature creature,
        RunRngSet runRng)
    {
        AssignCreature(creature);
        Rng = source.Rng.CloneExact();
        RunRng = runRng;
        IsPerformingMove = source.IsPerformingMove;
        SpawnedThisTurn = source.SpawnedThisTurn;
        if (source.MoveStateMachine is null)
        {
            return;
        }

        MoveStateMachine = GenerateMoveStateMachine();
    }

    internal void RestoreCombatCloneMoveStateFrom(MonsterModel source)
    {
        if (source.MoveStateMachine is null)
        {
            return;
        }

        if (MoveStateMachine is null)
        {
            throw new InvalidOperationException("Combat clone move state was not prepared.");
        }

        NextMove = MoveStateMachine.RestoreRuntimeStateFrom(
            source.MoveStateMachine,
            source.NextMove,
            this);
    }

    internal virtual void RestoreCombatCloneReferencesFrom(
        MonsterModel source,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
    }

    internal virtual void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
    }

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        AppendCombatStateDescription(ref builder, context);
}
