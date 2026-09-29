using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Models.Monsters;

/// <summary>Plan 04 最小内容集的占位怪物（Boss 档）。偏离 #47：不是真实 Overgrowth boss
/// （<c>VantomBoss</c>/<c>CeremonialBeastBoss</c>/<c>TheKinBoss</c>），真实内容移植到 Plan 06。</summary>
public sealed class ActOneGuardian : MonsterModel
{
    public override int MinInitialHp => 230;

    public override int MaxInitialHp => 250;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("CRUSH", CrushMove, new SingleAttackIntent(20));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }

    private async Task CrushMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(20m).FromMonster(this).Execute();
    }
}
