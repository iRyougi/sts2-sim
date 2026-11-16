using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;

namespace Sts2Sim.Core.Models.Monsters;

/// <summary>v0.113.1 Byrdine 宠物怪：9999 HP，NOTHING_MOVE 循环。偏离 #160：皮肤（发型/眼睛）省略。</summary>
public sealed class ByrdineMonster : MonsterModel
{
    public override int MinInitialHp => 9999;

    public override int MaxInitialHp => 9999;

    public override bool IsHealthBarVisible => false;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("NOTHING_MOVE", _ => Task.CompletedTask);
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }
}
