namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TwoTailedRat : MonsterModel
{
    private int _starterMoveIndex = -1;
    private int _turnsUntilSummonable = 2;
    private int _callForBackupCount;

    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 18, 17);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 22, 21);
    private int ScratchDamage => Value(AscensionLevel.DeadlyEnemies, 9, 8);
    private int DiseaseBiteDamage => Value(AscensionLevel.DeadlyEnemies, 7, 6);

    public int StarterMoveIndex
    {
        get => _starterMoveIndex;
        set
        {
            AssertMutable();
            _starterMoveIndex = value;
        }
    }

    public int CallForBackupCount
    {
        get => _callForBackupCount;
        set
        {
            AssertMutable();
            _callForBackupCount = value;
        }
    }

    private int TurnsUntilSummonable
    {
        get => _turnsUntilSummonable;
        set
        {
            AssertMutable();
            _turnsUntilSummonable = value;
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var scratch = new MoveState("SCRATCH_MOVE", Scratch, new SingleAttackIntent(() => ScratchDamage));
        var disease = new MoveState("DISEASE_BITE_MOVE", DiseaseBite, new SingleAttackIntent(() => DiseaseBiteDamage));
        var screech = new MoveState("SCREECH_MOVE", Screech, new DebuffIntent());
        var call = new MoveState("CALL_FOR_BACKUP_MOVE", CallForBackup, new SummonIntent());
        var random = new RandomBranchState("RAND");
        random.AddBranch(scratch, MoveRepeatType.CannotRepeat, () => CanSummon() ? 1f / 12f : 1f);
        random.AddBranch(disease, MoveRepeatType.CannotRepeat, () => CanSummon() ? 1f / 12f : 1f);
        random.AddBranch(screech, 3, MoveRepeatType.CannotRepeat, () => CanSummon() ? 1f / 12f : 1f);
        random.AddBranch(call, MoveRepeatType.UseOnlyOnce, () => CanSummon() ? 0.75f : 0f);
        scratch.FollowUpState = random;
        disease.FollowUpState = random;
        screech.FollowUpState = random;
        call.FollowUpState = random;
        if (StarterMoveIndex == -1)
        {
            return new MonsterMoveStateMachine([random, scratch, disease, screech, call], random);
        }

        MonsterState initial = (StarterMoveIndex % 3) switch
        {
            0 => scratch,
            1 => disease,
            _ => screech,
        };
        return new MonsterMoveStateMachine([random, scratch, disease, screech, call], initial);
    }

    private async Task Scratch(IReadOnlyList<Creature> _)
    {
        TurnsUntilSummonable--;
        await DamageCmd.Attack(ScratchDamage).FromMonster(this).Execute();
    }

    private async Task DiseaseBite(IReadOnlyList<Creature> _)
    {
        TurnsUntilSummonable--;
        await DamageCmd.Attack(DiseaseBiteDamage).FromMonster(this).Execute();
    }

    private async Task Screech(IReadOnlyList<Creature> targets)
    {
        TurnsUntilSummonable--;
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private async Task CallForBackup(IReadOnlyList<Creature> _)
    {
        ICombatState combatState = Creature.CombatState!;
        if (!combatState.IsLiveCombat())
        {
            return;
        }

        string? slot = NextRatSlot(combatState);
        if (slot is not null)
        {
            var rat = (TwoTailedRat)ModelDb.Monster<TwoTailedRat>().MutableClone();
            await CreatureCmd.Add(rat, combatState, CombatSide.Enemy, slot);
        }

        int nextCount = combatState.Enemies.Select(creature => creature.Monster)
            .OfType<TwoTailedRat>()
            .Select(rat => rat.CallForBackupCount + 1)
            .DefaultIfEmpty(CallForBackupCount + 1)
            .Max();
        foreach (TwoTailedRat rat in combatState.Enemies.Select(creature => creature.Monster).OfType<TwoTailedRat>())
        {
            rat.CallForBackupCount = nextCount;
        }
    }

    private bool CanSummon()
    {
        ICombatState? combatState = Creature.CombatState;
        if (combatState is null || TurnsUntilSummonable > 0 || CallForBackupCount >= 3 || NextRatSlot(combatState) is null)
        {
            return false;
        }

        return combatState.Enemies
            .Where(enemy => enemy != Creature && enemy.Monster is TwoTailedRat)
            .All(enemy => enemy.Monster!.NextMove?.StateId != "CALL_FOR_BACKUP_MOVE");
    }

    private static string? NextRatSlot(ICombatState combatState) =>
        new[] { "first", "second", "third", "fourth", "fifth" }
            .LastOrDefault(slot => combatState.Enemies.All(enemy => enemy.SlotName != slot));

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_starterMoveIndex);
        builder.Append(_turnsUntilSummonable);
        builder.Append(_callForBackupCount);
    }

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
