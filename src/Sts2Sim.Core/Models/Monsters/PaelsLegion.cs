using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;

namespace Sts2Sim.Core.Models.Monsters;

public sealed class PaelsLegion : MonsterModel
{
    public override int MinInitialHp => 9999;

    public override int MaxInitialHp => 9999;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("NOTHING_MOVE", _ => Task.CompletedTask);
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }
}
