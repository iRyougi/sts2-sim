namespace Sts2Sim.Core.Tests.MonsterMoves;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;

public class MonsterMoveStateMachineTests
{
    [Fact]
    public void RollMove_SelfLoopingMove_AlwaysReturnsTheSameMove()
    {
        var move = new MoveState("POKE", _ => Task.CompletedTask, new SingleAttackIntent(5));
        move.FollowUpState = move;
        var machine = new MonsterMoveStateMachine(new[] { move }, move);
        Creature owner = Creature.CreateStandaloneForTests(10, 10);
        var rng = new Rng(1uL);

        MoveState first = machine.RollMove(Array.Empty<Creature>(), owner, rng);
        machine.OnMovePerformed(first);
        MoveState second = machine.RollMove(Array.Empty<Creature>(), owner, rng);

        Assert.Same(move, first);
        Assert.Same(move, second);
    }

    [Fact]
    public void RollMove_TwoMoveCycle_AlternatesBetweenMoves()
    {
        var moveA = new MoveState("A", _ => Task.CompletedTask, new SingleAttackIntent(1));
        var moveB = new MoveState("B", _ => Task.CompletedTask, new SingleAttackIntent(2));
        moveA.FollowUpState = moveB;
        moveB.FollowUpState = moveA;
        var machine = new MonsterMoveStateMachine(new[] { moveA, moveB }, moveA);
        Creature owner = Creature.CreateStandaloneForTests(10, 10);
        var rng = new Rng(1uL);

        MoveState first = machine.RollMove(Array.Empty<Creature>(), owner, rng);
        machine.OnMovePerformed(first);
        MoveState second = machine.RollMove(Array.Empty<Creature>(), owner, rng);
        machine.OnMovePerformed(second);
        MoveState third = machine.RollMove(Array.Empty<Creature>(), owner, rng);

        Assert.Same(moveA, first);
        Assert.Same(moveB, second);
        Assert.Same(moveA, third);
    }

    [Fact]
    public async Task PerformMove_InvokesOnPerformDelegate_WithTargets()
    {
        Creature? seenTarget = null;
        var move = new MoveState("POKE", targets => { seenTarget = targets[0]; return Task.CompletedTask; }, new SingleAttackIntent(5));
        Creature target = Creature.CreateStandaloneForTests(10, 10);

        await move.PerformMove(new[] { target });

        Assert.Same(target, seenTarget);
    }

    [Fact]
    public void SingleAttackIntent_GetTotalDamage_ReturnsConfiguredDamage()
    {
        var intent = new SingleAttackIntent(7);
        Creature owner = Creature.CreateStandaloneForTests(10, 10);

        Assert.Equal(7, intent.GetTotalDamage(Array.Empty<Creature>(), owner));
        Assert.Equal(IntentType.Attack, intent.IntentType);
    }
}
