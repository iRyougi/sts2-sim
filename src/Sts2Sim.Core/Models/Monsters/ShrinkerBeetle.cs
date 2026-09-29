namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class ShrinkerBeetle : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 40, 38);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 42, 40);

    private int ChompDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 8, 7);

    private int StompDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 14, 13);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var shrink = new MoveState("SHRINKER_MOVE", ShrinkMove, new DebuffIntent(strong: true));
        var chomp = new MoveState("CHOMP_MOVE", ChompMove, new SingleAttackIntent(ChompDamage));
        var stomp = new MoveState("STOMP_MOVE", StompMove, new SingleAttackIntent(StompDamage));
        shrink.FollowUpState = chomp;
        chomp.FollowUpState = stomp;
        stomp.FollowUpState = chomp;
        return new MonsterMoveStateMachine(new MonsterState[] { shrink, chomp, stomp }, shrink);
    }

    private async Task ShrinkMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<ShrinkPower>(
                Creature.CombatState!,
                target,
                -1m,
                Creature,
                cardSource: null);
        }
    }

    private async Task ChompMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ChompDamage).FromMonster(this).Execute();
    }

    private async Task StompMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StompDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
