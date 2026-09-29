namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class FuzzyWurmCrawler : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 58, 55);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 59, 57);

    private int AcidGoopDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 6, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var firstAcidGoop = new MoveState(
            "FIRST_ACID_GOOP",
            AcidGoop,
            new SingleAttackIntent(AcidGoopDamage));
        var acidGoop = new MoveState(
            "ACID_GOOP",
            AcidGoop,
            new SingleAttackIntent(AcidGoopDamage));
        var inhale = new MoveState("INHALE", Inhale, new BuffIntent());
        firstAcidGoop.FollowUpState = inhale;
        inhale.FollowUpState = acidGoop;
        acidGoop.FollowUpState = firstAcidGoop;
        return new MonsterMoveStateMachine(
            new MonsterState[] { firstAcidGoop, acidGoop, inhale },
            firstAcidGoop);
    }

    private async Task AcidGoop(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(AcidGoopDamage).FromMonster(this).Execute();
    }

    private async Task Inhale(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            7m,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
