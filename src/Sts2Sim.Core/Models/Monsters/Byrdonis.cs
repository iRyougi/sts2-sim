namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Byrdonis : MonsterModel
{
    private const int PeckRepeat = 3;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 90, 81);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 90, 84);

    private int PeckDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 4, 3);

    private int SwoopDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 19, 17);

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        await PowerCmd.Apply<TerritorialPower>(
            Creature.CombatState!,
            Creature,
            1m,
            Creature,
            cardSource: null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var peck = new MoveState(
            "PECK_MOVE",
            PeckMove,
            new MultiAttackIntent(PeckDamage, PeckRepeat));
        var swoop = new MoveState(
            "SWOOP_MOVE",
            SwoopMove,
            new SingleAttackIntent(SwoopDamage));
        swoop.FollowUpState = peck;
        peck.FollowUpState = swoop;
        return new MonsterMoveStateMachine(new MonsterState[] { swoop, peck }, swoop);
    }

    private async Task PeckMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PeckDamage).WithHitCount(PeckRepeat).FromMonster(this).Execute();
    }

    private async Task SwoopMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SwoopDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
