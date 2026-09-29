namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Flyconid : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 51, 47);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 53, 49);

    private int SmashDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 12, 11);

    private int SporeDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 9, 8);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var vulnerableSpores = new MoveState(
            "VULNERABLE_SPORES_MOVE",
            VulnerableSporesMove,
            new DebuffIntent());
        var frailSpores = new MoveState(
            "FRAIL_SPORES_MOVE",
            FrailSporesMove,
            new SingleAttackIntent(SporeDamage),
            new DebuffIntent());
        var smash = new MoveState(
            "SMASH_MOVE",
            SmashMove,
            new SingleAttackIntent(SmashDamage));
        var random = new RandomBranchState("RAND");
        var initial = new RandomBranchState("INITIAL");
        vulnerableSpores.FollowUpState = random;
        frailSpores.FollowUpState = random;
        smash.FollowUpState = random;
        random.AddBranch(vulnerableSpores, cooldown: 3, MoveRepeatType.CannotRepeat);
        random.AddBranch(frailSpores, cooldown: 2, MoveRepeatType.CannotRepeat);
        random.AddBranch(smash, MoveRepeatType.CannotRepeat, 1f);
        initial.AddBranch(frailSpores, cooldown: 2, MoveRepeatType.CannotRepeat);
        initial.AddBranch(smash, MoveRepeatType.CannotRepeat, 1f);
        return new MonsterMoveStateMachine(
            new MonsterState[] { vulnerableSpores, frailSpores, smash, random, initial },
            initial);
    }

    private async Task VulnerableSporesMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(
                Creature.CombatState!,
                target,
                2m,
                Creature,
                cardSource: null);
        }
    }

    private async Task FrailSporesMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SporeDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<FrailPower>(
                Creature.CombatState!,
                target,
                2m,
                Creature,
                cardSource: null);
        }
    }

    private async Task SmashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SmashDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
