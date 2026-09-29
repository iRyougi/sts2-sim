namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class AssassinRubyRaider : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 19, 18);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 24, 23);

    private int KillshotDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 11, 10);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var killshot = new MoveState(
            "KILLSHOT_MOVE",
            KillshotMove,
            new SingleAttackIntent(KillshotDamage));
        killshot.FollowUpState = killshot;
        return new MonsterMoveStateMachine(new MonsterState[] { killshot }, killshot);
    }

    private async Task KillshotMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(KillshotDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
