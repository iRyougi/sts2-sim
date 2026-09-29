namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class ScrollOfBiting : MonsterModel
{
    public int StarterMoveIdx { get; set; }
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 33, 30);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 39, 37);
    private int Chomp => Value(AscensionLevel.DeadlyEnemies, 16, 14);
    private int Chew => Value(AscensionLevel.DeadlyEnemies, 6, 5);

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        await PowerCmd.Apply<PaperCutsPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var chomp = new MoveState("CHOMP", DoChomp, new SingleAttackIntent(Chomp));
        var chew = new MoveState("CHEW", DoChew, new MultiAttackIntent(Chew, 2));
        var teeth = new MoveState("MORE_TEETH", Teeth, new BuffIntent());
        var random = new RandomBranchState("rand");
        chomp.FollowUpState = teeth;
        teeth.FollowUpState = chew;
        chew.FollowUpState = random;
        random.AddBranch(chomp, MoveRepeatType.CannotRepeat);
        random.AddBranch(chew, 2);
        MonsterState initial = (StarterMoveIdx % 3) switch
        {
            0 => chomp,
            1 => chew,
            _ => teeth,
        };
        return new MonsterMoveStateMachine([chomp, chew, teeth, random], initial);
    }

    private Task DoChomp(IReadOnlyList<Creature> _) => DamageCmd.Attack(Chomp).FromMonster(this).Execute();
    private Task DoChew(IReadOnlyList<Creature> _) => DamageCmd.Attack(Chew).WithHitCount(2).FromMonster(this).Execute();
    private Task Teeth(IReadOnlyList<Creature> _) => PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
