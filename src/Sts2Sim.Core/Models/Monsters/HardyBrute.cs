using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Models.Monsters;

/// <summary>Plan 04 最小内容集的占位怪物（Elite 档）。偏离 #47：不是真实 Overgrowth 精英
/// （<c>ByrdonisElite</c>/<c>PhrogParasiteElite</c> 等），真实内容移植到 Plan 06。</summary>
public sealed class HardyBrute : MonsterModel
{
    public override int MinInitialHp => 130;

    public override int MaxInitialHp => 140;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("SLAM", SlamMove, new SingleAttackIntent(14));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }

    private async Task SlamMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(14m).FromMonster(this).Execute();
    }
}
