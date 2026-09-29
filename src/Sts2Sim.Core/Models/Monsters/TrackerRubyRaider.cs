namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TrackerRubyRaider : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 22, 21);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 26, 25);

    private int HoundsDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 1, 1);

    private int HoundsRepeat => AscensionValue(AscensionLevel.DeadlyEnemies, 9, 8);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var track = new MoveState("TRACK_MOVE", TrackMove, new DebuffIntent());
        var hounds = new MoveState(
            "HOUNDS_MOVE",
            HoundsMove,
            new MultiAttackIntent(HoundsDamage, HoundsRepeat));
        track.FollowUpState = hounds;
        hounds.FollowUpState = hounds;
        return new MonsterMoveStateMachine(new MonsterState[] { track, hounds }, track);
    }

    private async Task TrackMove(IReadOnlyList<Creature> targets)
    {
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

    private async Task HoundsMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(HoundsDamage)
            .WithHitCount(HoundsRepeat)
            .FromMonster(this)
            .Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
