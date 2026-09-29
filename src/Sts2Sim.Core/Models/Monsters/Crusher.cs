namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class Crusher : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 219, 209);
    public override int MaxInitialHp => MinInitialHp;
    private int ThrashDamage => Ascension(AscensionLevel.DeadlyEnemies, 14, 12);
    private int StingDamage => Ascension(AscensionLevel.DeadlyEnemies, 7, 6);
    private int AdaptStrength => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);
    private int GuardedDamage => Ascension(AscensionLevel.DeadlyEnemies, 14, 12);

    public override async Task AfterAddedToRoom()
    {
        await PowerCmd.Apply<BackAttackLeftPower>(Creature.CombatState!, Creature, 1m, Creature, null);
        await PowerCmd.Apply<CrabRagePower>(Creature.CombatState!, Creature, 1m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var thrash = new MoveState("THRASH_MOVE", _ => DamageCmd.Attack(ThrashDamage).FromMonster(this).Execute(), new SingleAttackIntent(ThrashDamage));
        var enlarge = new MoveState("ENLARGING_STRIKE_MOVE", _ => DamageCmd.Attack(4).FromMonster(this).Execute(), new SingleAttackIntent(4));
        var sting = new MoveState("BUG_STING_MOVE", BugSting, new MultiAttackIntent(StingDamage, 2), new DebuffIntent());
        var adapt = new MoveState("ADAPT_MOVE", _ => PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, AdaptStrength, Creature, null), new BuffIntent());
        var guarded = new MoveState("GUARDED_STRIKE_MOVE", Guarded, new SingleAttackIntent(GuardedDamage), new DefendIntent());
        thrash.FollowUpState = enlarge;
        enlarge.FollowUpState = sting;
        sting.FollowUpState = adapt;
        adapt.FollowUpState = guarded;
        guarded.FollowUpState = thrash;
        return new MonsterMoveStateMachine([thrash, enlarge, sting, adapt, guarded], thrash);
    }

    private async Task BugSting(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StingDamage).WithHitCount(2).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 2m, Creature, null);
            await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 2m, Creature, null);
        }
    }

    private async Task Guarded(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(GuardedDamage).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, 18m, ValueProp.Move, null, null);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
