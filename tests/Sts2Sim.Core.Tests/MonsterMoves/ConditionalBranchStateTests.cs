namespace Sts2Sim.Core.Tests.MonsterMoves;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;

[Collection("ModelDb")]
public class ConditionalBranchStateTests
{
    public ConditionalBranchStateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(ConditionalBranchTestMonster) });
    }


    [Fact]
    public void GetNextState_MultipleMatchingBranches_ReturnsTheFirstMatchInAddedOrder()
    {
        var subject = new ConditionalBranchState("INITIAL")
            .AddBranch(_ => true, "FIRST")
            .AddBranch(_ => true, "SECOND");

        string nextState = subject.GetNextState(CreateOwner(), new Rng(1uL));

        Assert.Equal("FIRST", nextState);
    }

    [Fact]
    public void GetNextState_FalseBranchBeforeTrueBranch_SkipsTheFalseBranch()
    {
        var subject = new ConditionalBranchState("INITIAL")
            .AddBranch(_ => false, "SKIPPED")
            .AddBranch(_ => true, "MATCHED");

        string nextState = subject.GetNextState(CreateOwner(), new Rng(1uL));

        Assert.Equal("MATCHED", nextState);
    }

    [Fact]
    public void GetNextState_NoMatchingBranch_ThrowsDescriptiveError()
    {
        var subject = new ConditionalBranchState("INITIAL")
            .AddBranch(_ => false, "UNREACHABLE");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => subject.GetNextState(CreateOwner(), new Rng(1uL)));

        Assert.Equal("No branch predicate matched for ConditionalBranchState 'INITIAL'.", error.Message);
    }

    [Fact]
    public void State_IsAnInvisibleNonMove()
    {
        var subject = new ConditionalBranchState("INITIAL");

        Assert.False(subject.IsMove);
        Assert.False(subject.ShouldAppearInLogs);
    }

    [Fact]
    public void RegisterStates_AddsThisStateUnderItsId()
    {
        var subject = new ConditionalBranchState("INITIAL");
        var states = new Dictionary<string, MonsterState>();

        subject.RegisterStates(states);

        Assert.Same(subject, states["INITIAL"]);
    }

    private static Creature CreateOwner()
    {
        var monster = (ConditionalBranchTestMonster)ModelDb.Monster<ConditionalBranchTestMonster>().MutableClone();
        var owner = new Creature(monster, CombatSide.Enemy);
        monster.AssignCreature(owner);
        return owner;
    }

    private sealed class ConditionalBranchTestMonster : MonsterModel
    {
        public override int MinInitialHp => 1;

        public override int MaxInitialHp => 1;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var move = new MoveState("TEST", _ => Task.CompletedTask, new SingleAttackIntent(1));
            move.FollowUpState = move;
            return new MonsterMoveStateMachine(new[] { move }, move);
        }
    }
}
