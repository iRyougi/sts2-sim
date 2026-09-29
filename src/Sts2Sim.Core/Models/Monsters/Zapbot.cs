namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Zapbot : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 19, 18);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 24, 23);
    private int Damage => Ascension(AscensionLevel.DeadlyEnemies, 15, 14);
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<HighVoltagePower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var zap = new MoveState("ZAP", Move, new SingleAttackIntent(Damage)); zap.FollowUpState = zap;
        return new MonsterMoveStateMachine(new MonsterState[] { zap }, zap);
    }
    private Task Move(IReadOnlyList<Creature> targets) => DamageCmd.Attack(Damage).FromMonster(this).Execute();
    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
