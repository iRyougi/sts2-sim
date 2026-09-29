namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SlumberingBeetle : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 89, 86);
    public override int MaxInitialHp => MinInitialHp;
    private int RollDamage => Ascension(AscensionLevel.DeadlyEnemies, 18, 16);
    private int Plating => Ascension(AscensionLevel.ToughEnemies, 18, 15);

    public override async Task AfterAddedToRoom()
    {
        await PowerCmd.Apply<PlatingPower>(Creature.CombatState!, Creature, Plating, Creature, null);
        await PowerCmd.Apply<SlumberPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var snore = new MoveState("SNORE_MOVE", _ => Task.CompletedTask, new SleepIntent());
        var roll = new MoveState("ROLL_OUT_MOVE", RollOut, new SingleAttackIntent(RollDamage), new BuffIntent());
        var branch = new ConditionalBranchState("SLEEP_CHECK")
            .AddBranch(creature => creature.HasPower<SlumberPower>(), snore.Id)
            .AddBranch(_ => true, roll.Id);
        snore.FollowUpState = branch;
        roll.FollowUpState = roll;
        return new MonsterMoveStateMachine([branch, snore, roll], branch);
    }

    public async Task WakeUp()
    {
        if (Creature.GetPower<PlatingPower>() is { } plating) await PowerCmd.Remove(plating);
    }

    internal void SetWakeUpStunned() =>
        SetMoveImmediate(CreateWakeUpStunnedMove(), forceTransition: true);

    private MoveState CreateWakeUpStunnedMove() => new("STUNNED", _ => WakeUp(), new StunIntent())
    {
        FollowUpStateId = "ROLL_OUT_MOVE",
        MustPerformOnceBeforeTransitioning = true,
        CombatCloneFactory = cloned => ((SlumberingBeetle)cloned).CreateWakeUpStunnedMove(),
    };

    public void SetRollOut()
    {
        MoveState roll = MoveStateMachine!.States.Values.OfType<MoveState>().Single(state => state.Id == "ROLL_OUT_MOVE");
        SetMoveImmediate(roll, true);
    }

    private async Task RollOut(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(RollDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
