namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class LivingShield : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 65, 55);
    public override int MaxInitialHp => MinInitialHp;
    private int Smash => Value(AscensionLevel.DeadlyEnemies, 18, 16);

    public override Task BeforeCombatStart() =>
        PowerCmd.Apply<RampartPower>(Creature.CombatState!, Creature, 25m, Creature, null);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var slam = new MoveState("SHIELD_SLAM_MOVE", Slam, new SingleAttackIntent(6));
        var branch = new ConditionalBranchState("SHIELD_SLAM_BRANCH");
        var smash = new MoveState("SMASH_MOVE", SmashMove, new SingleAttackIntent(Smash), new BuffIntent());
        slam.FollowUpState = branch;
        branch.AddBranch(creature => creature.CombatState!.Enemies.Any(enemy => enemy.IsAlive && enemy != creature), slam.Id);
        branch.AddBranch(creature => !creature.CombatState!.Enemies.Any(enemy => enemy.IsAlive && enemy != creature), smash.Id);
        smash.FollowUpState = smash;
        return new MonsterMoveStateMachine([slam, branch, smash], slam);
    }

    private Task Slam(IReadOnlyList<Creature> _) => DamageCmd.Attack(6).FromMonster(this).Execute();

    private async Task SmashMove(IReadOnlyList<Creature> _)
    {
        await DamageCmd.Attack(Smash).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
