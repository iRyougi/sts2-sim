namespace Sts2Sim.Core.Tests.MonsterMoves;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class ForcedStateTransitionTests
{
    public ForcedStateTransitionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(ForcedTransitionTestMonster) });
    }

    [Fact]
    public async Task SetMoveImmediate_InterruptibleMove_SetsTheRequestedMoveForTheNextRoll()
    {
        ForcedTransitionTestMonster monster = CreateMonster();
        MoveState stateA = GetState(monster, "A");
        MoveState stateB = GetState(monster, "B");

        monster.RollMove(Array.Empty<Creature>());
        await stateA.PerformMove(Array.Empty<Creature>());
        monster.MoveStateMachine!.OnMovePerformed(stateA);
        monster.SetMoveImmediate(stateB);

        Assert.Same(stateB, monster.NextMove);
        monster.RollMove(Array.Empty<Creature>());
        Assert.Same(stateB, monster.NextMove);
    }

    [Fact]
    public void SetMoveImmediate_UninterruptibleMove_DoesNotChangeTheCurrentMove()
    {
        ForcedTransitionTestMonster monster = CreateMonster();
        MoveState stateA = GetState(monster, "A");
        MoveState stateB = GetState(monster, "B");

        monster.RollMove(Array.Empty<Creature>());
        monster.SetMoveImmediate(stateB);

        Assert.Same(stateA, monster.NextMove);
        monster.RollMove(Array.Empty<Creature>());
        Assert.Same(stateA, monster.NextMove);
    }

    [Fact]
    public void SetMoveImmediate_ForcedFromUninterruptibleMove_SetsTheRequestedMove()
    {
        ForcedTransitionTestMonster monster = CreateMonster();
        MoveState stateB = GetState(monster, "B");

        monster.RollMove(Array.Empty<Creature>());
        monster.SetMoveImmediate(stateB, forceTransition: true);

        Assert.Same(stateB, monster.NextMove);
        monster.RollMove(Array.Empty<Creature>());
        Assert.Same(stateB, monster.NextMove);
    }

    [Fact]
    public async Task SetMoveImmediate_ForcedTransition_BypassesOriginalFollowUpAndPreservesSubsequentStateLogging()
    {
        ForcedTransitionTestMonster monster = CreateMonster();
        MoveState stateA = GetState(monster, "A");
        MoveState stateB = GetState(monster, "B");
        MoveState originalFollowUp = GetState(monster, "ORIGINAL");

        monster.RollMove(Array.Empty<Creature>());
        await stateA.PerformMove(Array.Empty<Creature>());
        monster.MoveStateMachine!.OnMovePerformed(stateA);
        monster.SetMoveImmediate(stateB, forceTransition: true);
        monster.RollMove(Array.Empty<Creature>());

        Assert.Same(stateB, monster.NextMove);
        Assert.DoesNotContain(originalFollowUp, monster.MoveStateMachine!.StateLog);
        Assert.Equal(new MonsterState[] { stateA, stateB }, monster.MoveStateMachine.StateLog);

        await stateB.PerformMove(Array.Empty<Creature>());
        monster.MoveStateMachine.OnMovePerformed(stateB);
        monster.RollMove(Array.Empty<Creature>());

        Assert.Same(stateB, monster.NextMove);
        Assert.Equal(new MonsterState[] { stateA, stateB, stateB }, monster.MoveStateMachine.StateLog);
    }

    private static ForcedTransitionTestMonster CreateMonster()
    {
        var monster = (ForcedTransitionTestMonster)ModelDb.Monster<ForcedTransitionTestMonster>().MutableClone();
        var creature = new Creature(monster, CombatSide.Enemy);
        monster.AssignCreature(creature);
        monster.RunRng = new RunRngSet("forced-state-transition-tests");
        monster.SetUpForCombat();
        return monster;
    }

    private static MoveState GetState(ForcedTransitionTestMonster monster, string stateId)
    {
        return (MoveState)monster.MoveStateMachine!.States[stateId];
    }

    private sealed class ForcedTransitionTestMonster : MonsterModel
    {
        public override int MinInitialHp => 1;

        public override int MaxInitialHp => 1;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var stateA = new MoveState("A", _ => Task.CompletedTask, new SingleAttackIntent(1))
            {
                MustPerformOnceBeforeTransitioning = true,
            };
            var stateB = new MoveState("B", _ => Task.CompletedTask, new SingleAttackIntent(2));
            var originalFollowUp = new MoveState("ORIGINAL", _ => Task.CompletedTask, new SingleAttackIntent(3));
            stateA.FollowUpState = originalFollowUp;
            stateB.FollowUpState = stateB;
            originalFollowUp.FollowUpState = originalFollowUp;
            return new MonsterMoveStateMachine(new MonsterState[] { stateA, stateB, originalFollowUp }, stateA);
        }
    }
}
