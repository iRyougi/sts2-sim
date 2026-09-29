namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public class FlailKnight : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 108, 101);
    public override int MaxInitialHp => MinInitialHp;
    private int FlailDamage => Value(AscensionLevel.DeadlyEnemies, 10, 9);
    private int RamDamage => Value(AscensionLevel.DeadlyEnemies, 17, 15);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var chant = new MoveState("WAR_CHANT", Chant, new BuffIntent());
        var flail = new MoveState("FLAIL_MOVE", Flail, new MultiAttackIntent(FlailDamage, 2));
        var ram = new MoveState("RAM_MOVE", Ram, new SingleAttackIntent(RamDamage));
        var random = new RandomBranchState("RAND");
        chant.FollowUpState = random;
        flail.FollowUpState = random;
        ram.FollowUpState = random;
        random.AddBranch(chant, MoveRepeatType.CannotRepeat);
        random.AddBranch(flail, 2);
        random.AddBranch(ram, 2);
        return new MonsterMoveStateMachine([chant, flail, ram, random], ram);
    }

    private Task Chant(IReadOnlyList<Creature> _) => PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    private Task Flail(IReadOnlyList<Creature> _) => DamageCmd.Attack(FlailDamage).WithHitCount(2).FromMonster(this).Execute();
    private Task Ram(IReadOnlyList<Creature> _) => DamageCmd.Attack(RamDamage).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
