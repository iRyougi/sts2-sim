namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class CubexConstruct : MonsterModel
{
    private const int ExpelRepeat = 2;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 70, 65);

    public override int MaxInitialHp => MinInitialHp;

    private int BlastDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 8, 7);

    private int ExpelDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 6, 5);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await CreatureCmd.GainBlock(
            Creature.CombatState!,
            Creature,
            13m,
            ValueProp.Move,
            cardSource: null,
            cardPlay: null);
        await PowerCmd.Apply<ArtifactPower>(
            Creature.CombatState!,
            Creature,
            1m,
            Creature,
            cardSource: null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var charge = new MoveState("CHARGE_UP_MOVE", ChargeUpMove, new BuffIntent());
        var blastOne = new MoveState(
            "REPEATER_BLAST_MOVE",
            RepeaterBlastMove,
            new SingleAttackIntent(BlastDamage),
            new BuffIntent());
        var blastTwo = new MoveState(
            "REPEATER_BLAST_MOVE_2",
            RepeaterBlastMove,
            new SingleAttackIntent(BlastDamage),
            new BuffIntent());
        var expel = new MoveState(
            "EXPEL_MOVE",
            ExpelBlastMove,
            new MultiAttackIntent(ExpelDamage, ExpelRepeat));
        charge.FollowUpState = blastOne;
        blastOne.FollowUpState = blastTwo;
        blastTwo.FollowUpState = expel;
        expel.FollowUpState = blastOne;
        return new MonsterMoveStateMachine(
            new MonsterState[] { charge, blastOne, blastTwo, expel },
            charge);
    }

    private async Task ChargeUpMove(IReadOnlyList<Creature> targets)
    {
        await ApplyStrength();
    }

    private async Task RepeaterBlastMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BlastDamage).FromMonster(this).Execute();
        await ApplyStrength();
    }

    private async Task ExpelBlastMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ExpelDamage).WithHitCount(ExpelRepeat).FromMonster(this).Execute();
    }

    private Task ApplyStrength() => PowerCmd.Apply<StrengthPower>(
        Creature.CombatState!,
        Creature,
        2m,
        Creature,
        cardSource: null);

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
