namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TorchHeadAmalgam : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 211, 199);

    public override int MaxInitialHp => MinInitialHp;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var strongTackle = new MoveState(
            "STRONG_TACKLE_MOVE", StrongTackle, new SingleAttackIntent(StrongTackleDamage));
        var tackle = new MoveState("TACKLE_2_MOVE", Tackle, new SingleAttackIntent(TackleDamage));
        var beam = new MoveState("BEAM_MOVE", Beam, new MultiAttackIntent(BeamDamage, 3));
        var firstWeakTackle = new MoveState(
            "TACKLE_3_MOVE", WeakTackle, new SingleAttackIntent(WeakTackleDamage));
        var secondWeakTackle = new MoveState(
            "TACKLE_4_MOVE", WeakTackle, new SingleAttackIntent(WeakTackleDamage));

        strongTackle.FollowUpState = tackle;
        tackle.FollowUpState = beam;
        beam.FollowUpState = firstWeakTackle;
        firstWeakTackle.FollowUpState = secondWeakTackle;
        secondWeakTackle.FollowUpState = beam;
        return new MonsterMoveStateMachine(
            [strongTackle, tackle, beam, firstWeakTackle, secondWeakTackle],
            strongTackle);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<MinionPower>(Creature.CombatState!, Creature, 1m, Creature, null);
    }

    private int StrongTackleDamage => Ascension(AscensionLevel.DeadlyEnemies, 32, 26);
    private int TackleDamage => Ascension(AscensionLevel.DeadlyEnemies, 22, 18);
    private const int BeamDamage = 8;
    private int WeakTackleDamage => Ascension(AscensionLevel.DeadlyEnemies, 16, 14);

    private Task StrongTackle(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(StrongTackleDamage).FromMonster(this).Execute();

    private Task Tackle(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(TackleDamage).FromMonster(this).Execute();

    private Task Beam(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(BeamDamage).WithHitCount(3).FromMonster(this).Execute();

    private Task WeakTackle(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(WeakTackleDamage).FromMonster(this).Execute();

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
