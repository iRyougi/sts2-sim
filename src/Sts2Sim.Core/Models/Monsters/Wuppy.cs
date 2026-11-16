using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;

namespace Sts2Sim.Core.Models.Monsters;

/// <summary>v0.113.1 宠物怪 Wuppy：名义 50 HP（实际 HP 由 <see cref="Relics.Wuppy"/> 覆盖），NOTHING_MOVE 循环。</summary>
public sealed class Wuppy : MonsterModel
{
    public override int MinInitialHp => 50;

    public override int MaxInitialHp => 50;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("NOTHING_MOVE", _ => Task.CompletedTask);
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }
}
