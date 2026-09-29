namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class TheObscura : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 129, 123);
    public override int MaxInitialHp => MinInitialHp;
    private int GazeDamage => Ascension(AscensionLevel.DeadlyEnemies, 11, 10);
    private int HardeningAmount => Ascension(AscensionLevel.DeadlyEnemies, 7, 6);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var illusion = new MoveState("ILLUSION_MOVE", Illusion, new SummonIntent());
        var gaze = new MoveState("PIERCING_GAZE_MOVE", _ => DamageCmd.Attack(GazeDamage).FromMonster(this).Execute(), new SingleAttackIntent(GazeDamage));
        var wail = new MoveState("SAIL_MOVE", Wail, new BuffIntent());
        var hardening = new MoveState("HARDENING_STRIKE_MOVE", Hardening, new SingleAttackIntent(HardeningAmount), new DefendIntent());
        var random = new RandomBranchState("RANDOM_MOVE");
        random.AddBranch(gaze, MoveRepeatType.CannotRepeat);
        random.AddBranch(wail, MoveRepeatType.CannotRepeat);
        random.AddBranch(hardening, MoveRepeatType.CannotRepeat);
        illusion.FollowUpState = random;
        gaze.FollowUpState = random;
        wail.FollowUpState = random;
        hardening.FollowUpState = random;
        return new MonsterMoveStateMachine([illusion, random, gaze, wail, hardening], illusion);
    }

    private async Task Illusion(IReadOnlyList<Creature> targets)
    {
        var parafright = (Parafright)ModelDb.Monster<Parafright>().MutableClone();
        await CreatureCmd.Add(parafright, Creature.CombatState!, Creature.Side, "illusion");
    }

    private async Task Wail(IReadOnlyList<Creature> targets)
    {
        foreach (Creature teammate in Creature.CombatState!.GetCreaturesOnSide(Creature.Side).Where(c => !c.IsDead))
            await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, teammate, 3m, Creature, null);
    }

    private async Task Hardening(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(HardeningAmount).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, HardeningAmount, ValueProp.Move, null, null);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
