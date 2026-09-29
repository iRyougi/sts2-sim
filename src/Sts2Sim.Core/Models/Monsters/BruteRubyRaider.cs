namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class BruteRubyRaider : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 31, 30);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 34, 33);

    private int BeatDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 8, 7);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var beat = new MoveState("BEAT_MOVE", BeatMove, new SingleAttackIntent(BeatDamage));
        var roar = new MoveState("ROAR_MOVE", RoarMove, new BuffIntent());
        beat.FollowUpState = roar;
        roar.FollowUpState = beat;
        return new MonsterMoveStateMachine(new MonsterState[] { beat, roar }, beat);
    }

    private async Task BeatMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BeatDamage).FromMonster(this).Execute();
    }

    private async Task RoarMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            3m,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
