using Sts2Sim.Core.Models;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Tests.Models.Potions;

public sealed class Task11RerollTrackingMonster : MonsterModel
{
    public override int MinInitialHp => 40;
    public override int MaxInitialHp => 40;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var first = new MoveState("FIRST", _ => Task.CompletedTask, new SingleAttackIntent(0));
        var left = new MoveState("LEFT", _ => Task.CompletedTask, new SingleAttackIntent(0));
        var right = new MoveState("RIGHT", _ => Task.CompletedTask, new SingleAttackIntent(0));
        var decision = new Task11RandomDecisionState(left, right);
        first.FollowUpState = decision;
        left.FollowUpState = left;
        right.FollowUpState = right;
        return new MonsterMoveStateMachine(new MonsterState[] { first, decision }, first);
    }

}
