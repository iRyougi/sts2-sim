namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class Tunneler : MonsterModel
{
    public bool IsStunned { get; private set; }
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 92, 87);
    public override int MaxInitialHp => MinInitialHp;
    private int BiteDamage => Ascension(AscensionLevel.DeadlyEnemies, 15, 13);
    private int BurrowBlock => Ascension(AscensionLevel.ToughEnemies, 37, 32);
    private int BelowDamage => Ascension(AscensionLevel.DeadlyEnemies, 26, 23);

    public void GetStunned() => IsStunned = true;

    internal void SetBurrowBreakStunned()
    {
        if (Creature.CombatState is not null && !Creature.IsDead)
            SetMoveImmediate(CreateBurrowBreakStunnedMove());
    }

    private MoveState CreateBurrowBreakStunnedMove() => new("STUNNED", StillDizzyMove, new StunIntent())
    {
        FollowUpStateId = "BITE_MOVE",
        MustPerformOnceBeforeTransitioning = true,
        CombatCloneFactory = cloned => ((Tunneler)cloned).CreateBurrowBreakStunnedMove(),
    };

    private Task StillDizzyMove(IReadOnlyList<Creature> targets)
    {
        IsStunned = false;
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bite = new MoveState("BITE_MOVE", _ =>
        {
            IsStunned = false;
            return DamageCmd.Attack(BiteDamage).FromMonster(this).Execute();
        }, new SingleAttackIntent(BiteDamage));
        var burrow = new MoveState("BURROW_MOVE", Burrow, new BuffIntent(), new DefendIntent());
        var below = new MoveState("BELOW_MOVE", _ =>
        {
            IsStunned = false;
            return DamageCmd.Attack(BelowDamage).FromMonster(this).Execute();
        }, new SingleAttackIntent(BelowDamage));
        bite.FollowUpState = burrow;
        burrow.FollowUpState = below;
        below.FollowUpState = below;
        return new MonsterMoveStateMachine([bite, burrow, below], bite);
    }

    private async Task Burrow(IReadOnlyList<Creature> targets)
    {
        IsStunned = false;
        await PowerCmd.Apply<BurrowedPower>(Creature.CombatState!, Creature, 1m, Creature, null);
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, BurrowBlock, ValueProp.Move, null, null);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
