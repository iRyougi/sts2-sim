namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class Guardbot : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 17, 16);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 21, 20);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var guard = new MoveState("GUARD_MOVE", Guard, new DefendIntent()); guard.FollowUpState = guard;
        return new MonsterMoveStateMachine(new MonsterState[] { guard }, guard);
    }
    private async Task Guard(IReadOnlyList<Creature> targets)
    {
        foreach (Creature ally in Creature.CombatState!.Enemies.Where(enemy => enemy.Monster is Fabricator && enemy.IsAlive))
        {
            await CreatureCmd.GainBlock(Creature.CombatState, ally, 15m, ValueProp.Unpowered, null, null);
        }
    }
    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
