using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Tests.Models.Cards;

public sealed class Task11NoDamageMonster : MonsterModel
{
    public override int MinInitialHp => 40;
    public override int MaxInitialHp => 40;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState(
            "WAIT",
            _ => Task.CompletedTask,
            new SingleAttackIntent(0));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }
}
