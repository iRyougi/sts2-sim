namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Entomancer : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 165, 145);
    public override int MaxInitialHp => MinInitialHp;
    private int SpearDamage => Ascension(AscensionLevel.DeadlyEnemies, 20, 18);
    private int BeesRepeats => Ascension(AscensionLevel.DeadlyEnemies, 8, 7);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<PersonalHivePower>(Creature.CombatState!, Creature, 1m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var pheromone = new MoveState("PHEROMONE_SPIT_MOVE", Pheromone, new BuffIntent());
        var bees = new MoveState("BEES_MOVE", Bees, new MultiAttackIntent(3, BeesRepeats));
        var spear = new MoveState("SPEAR_MOVE", Spear, new SingleAttackIntent(SpearDamage));
        pheromone.FollowUpState = bees;
        bees.FollowUpState = spear;
        spear.FollowUpState = pheromone;
        return new MonsterMoveStateMachine(new MonsterState[] { pheromone, bees, spear }, bees);
    }

    private async Task Pheromone(IReadOnlyList<Creature> targets)
    {
        PersonalHivePower? hive = Creature.GetPower<PersonalHivePower>();
        if (hive is null || hive.Amount >= 3)
        {
            await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);
            return;
        }

        await PowerCmd.Apply<PersonalHivePower>(Creature.CombatState!, Creature, 1m, Creature, null);
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 1m, Creature, null);
    }

    private Task Bees(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(3).WithHitCount(BeesRepeats).FromMonster(this).Execute();

    private Task Spear(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(SpearDamage).FromMonster(this).Execute();

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
