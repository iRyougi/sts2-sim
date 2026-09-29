namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class BattleFriendV3 : MonsterModel
{
    public override int MinInitialHp => 300;

    public override int MaxInitialHp => 300;

    public override Task AfterAddedToRoom() =>
        PowerCmd.Apply<BattlewornDummyTimeLimitPower>(Creature.CombatState!, Creature, 3m, Creature, null);

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Creature.Side && Creature.GetPower<BattlewornDummyTimeLimitPower>()?.ShouldEscape == true)
        {
            return CreatureCmd.Escape(Creature);
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var idle = new MoveState("IDLE", _ => Task.CompletedTask, new StunIntent());
        idle.FollowUpState = idle;
        return new MonsterMoveStateMachine([idle], idle);
    }
}
