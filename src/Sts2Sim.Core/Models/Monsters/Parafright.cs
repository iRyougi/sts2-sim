namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Parafright : MonsterModel
{
    public override int MinInitialHp => 21;
    public override int MaxInitialHp => 21;
    private int SlamDamage => Ascension(AscensionLevel.DeadlyEnemies, 17, 16);

    public override async Task AfterAddedToRoom() =>
        await PowerCmd.Apply<IllusionPower>(Creature.CombatState!, Creature, 1m, Creature, null);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var slam = new MoveState("SLAM_MOVE", _ => DamageCmd.Attack(SlamDamage).FromMonster(this).Execute(), new SingleAttackIntent(SlamDamage));
        slam.FollowUpState = slam;
        return new MonsterMoveStateMachine([slam], slam);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
