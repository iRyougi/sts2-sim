namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class BowlbugSilk : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 41, 40);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 44, 43);
    private int Damage => Ascension(AscensionLevel.DeadlyEnemies, 5, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var thrash = new MoveState("THRASH_MOVE", Thrash, new MultiAttackIntent(Damage, 2));
        var spit = new MoveState("TOXIC_SPIT_MOVE", Spit, new DebuffIntent());
        thrash.FollowUpState = spit;
        spit.FollowUpState = thrash;
        return new MonsterMoveStateMachine(new MonsterState[] { thrash, spit }, spit);
    }

    private Task Thrash(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(Damage).WithHitCount(2).FromMonster(this).Execute();

    private async Task Spit(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
