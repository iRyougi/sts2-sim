namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class FatGremlin : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 14, 13);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 18, 17);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var spawned = new MoveState("SPAWNED_MOVE", Spawned, new StunIntent());
        var flee = new MoveState("FLEE_MOVE", Flee, new EscapeIntent());
        spawned.FollowUpState = flee;
        flee.FollowUpState = flee;
        return new MonsterMoveStateMachine([spawned, flee], spawned);
    }

    private Task Spawned(IReadOnlyList<Creature> _) => Task.CompletedTask;

    private Task Flee(IReadOnlyList<Creature> _) => CreatureCmd.Escape(Creature);

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
