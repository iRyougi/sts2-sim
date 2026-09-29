using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Models.Monsters;

/// <summary>
/// console demo 用的测试怪物。偏离 #36：不是真实游戏内容——用游戏自带调试怪物
/// （<c>SingleAttackMoveMonster</c>）同款的单招自循环架构搭建,数值调成"能被 8 张基础卡的储君打死"。
/// </summary>
public sealed class TrainingDummy : MonsterModel
{
    public override int MinInitialHp => 20;

    public override int MaxInitialHp => 20;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("POKE", PokeMove, new SingleAttackIntent(8));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }

    private async Task PokeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(8m).FromMonster(this).Execute();
    }
}
