namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class HunterKiller : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 126, 121);
    public override int MaxInitialHp => MinInitialHp;
    private int BiteDamage => Ascension(AscensionLevel.DeadlyEnemies, 19, 17);
    private int PunctureDamage => Ascension(AscensionLevel.DeadlyEnemies, 8, 7);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var goop = new MoveState("TENDERIZING_GOOP_MOVE", Goop, new DebuffIntent());
        var bite = new MoveState("BITE_MOVE", Bite, new SingleAttackIntent(BiteDamage));
        var puncture = new MoveState("PUNCTURE_MOVE", Puncture, new MultiAttackIntent(PunctureDamage, 3));
        var random = new RandomBranchState("RAND");
        random.AddBranch(bite, MoveRepeatType.CannotRepeat);
        random.AddBranch(puncture, 2);
        goop.FollowUpState = random;
        bite.FollowUpState = random;
        puncture.FollowUpState = random;
        return new MonsterMoveStateMachine(new MonsterState[] { goop, bite, puncture, random }, goop);
    }

    private async Task Goop(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<TenderPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private Task Bite(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(BiteDamage).FromMonster(this).Execute();

    private Task Puncture(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(PunctureDamage).WithHitCount(3).FromMonster(this).Execute();

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
