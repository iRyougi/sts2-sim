namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Stabbot : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 19, 18);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 24, 23);
    private int Damage => Ascension(AscensionLevel.DeadlyEnemies, 12, 11);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var stab = new MoveState("STAB_MOVE", Move, new SingleAttackIntent(Damage), new DebuffIntent()); stab.FollowUpState = stab;
        return new MonsterMoveStateMachine(new MonsterState[] { stab }, stab);
    }
    private async Task Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Damage).FromMonster(this).Execute();
        foreach (Creature target in targets) await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 1m, Creature, null);
    }
    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
