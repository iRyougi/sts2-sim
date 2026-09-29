using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Models.Monsters;

/// <summary>
/// Minimal Plan 04 monster model.
/// Deviation #47: this is not a real Overgrowth encounter monster; the real 22 encounters
/// such as <c>NibbitsWeak</c> and <c>SlimesNormal</c> move to Plan 06.
/// Values are calibrated against the real Act 1 normal-monster HP range and the Regent starter deck.
/// </summary>
public sealed class WanderingGrunt : MonsterModel
{
    public override int MinInitialHp => 42;

    public override int MaxInitialHp => 48;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("BITE", BiteMove, new SingleAttackIntent(7));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }

    private async Task BiteMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(7m).FromMonster(this).Execute();
    }
}
