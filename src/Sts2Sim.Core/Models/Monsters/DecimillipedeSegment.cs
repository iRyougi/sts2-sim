namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public abstract class DecimillipedeSegment : MonsterModel
{
    private int _starterMoveIdx;

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 46, 40);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 52, 46);
    private int WritheDamage => Ascension(AscensionLevel.DeadlyEnemies, 6, 5);
    private int BulkDamage => Ascension(AscensionLevel.DeadlyEnemies, 7, 6);
    private int ConstrictDamage => Ascension(AscensionLevel.DeadlyEnemies, 9, 8);

    public int StarterMoveIdx
    {
        get => _starterMoveIdx;
        set
        {
            AssertMutable();
            _starterMoveIdx = value;
        }
    }

    public MoveState DeadState { get; private set; } = null!;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        decimal maxHp = Creature.MaxHp;
        if (maxHp % 2m == 1m)
        {
            maxHp++;
        }

        Creature[] teammates = Creature.CombatState!
            .GetCreaturesOnSide(Creature.Side)
            .Where(candidate => !ReferenceEquals(candidate, Creature))
            .ToArray();
        while (teammates.Any(candidate => candidate.MaxHp == maxHp))
        {
            maxHp += 2m;
            if (maxHp > MaxInitialHp)
            {
                maxHp = MinInitialHp;
            }
        }

        Creature.SetMaxHpInternal(maxHp);
        await CreatureCmd.Heal(Creature, maxHp);
        await PowerCmd.Apply<ReattachPower>(Creature.CombatState!, Creature, 25m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var writhe = new MoveState("WRITHE_MOVE", Writhe, new MultiAttackIntent(WritheDamage, 2));
        var bulk = new MoveState("BULK_MOVE", Bulk, new SingleAttackIntent(BulkDamage), new BuffIntent());
        var constrict = new MoveState("CONSTRICT_MOVE", Constrict, new SingleAttackIntent(ConstrictDamage), new DebuffIntent());
        DeadState = new MoveState("DEAD_MOVE", _ => Task.CompletedTask);
        var reattach = new MoveState("REATTACH_MOVE", Reattach, new HealIntent())
        {
            MustPerformOnceBeforeTransitioning = true,
        };
        var random = new RandomBranchState("RAND");
        random.AddBranch(writhe, MoveRepeatType.CannotRepeat);
        random.AddBranch(bulk, MoveRepeatType.CannotRepeat);
        random.AddBranch(constrict, MoveRepeatType.CannotRepeat);
        writhe.FollowUpState = constrict;
        constrict.FollowUpState = bulk;
        bulk.FollowUpState = writhe;
        DeadState.FollowUpState = reattach;
        reattach.FollowUpState = random;
        MoveState initial = (StarterMoveIdx % 3) switch { 0 => writhe, 1 => bulk, _ => constrict };
        return new MonsterMoveStateMachine(
            new MonsterState[] { writhe, bulk, constrict, DeadState, reattach, random },
            initial);
    }

    private Task Writhe(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(WritheDamage).WithHitCount(2).FromMonster(this).Execute();

    private async Task Bulk(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BulkDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }

    private async Task Constrict(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ConstrictDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private Task Reattach(IReadOnlyList<Creature> targets) =>
        Creature.GetPower<ReattachPower>()!.DoReattach();

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
