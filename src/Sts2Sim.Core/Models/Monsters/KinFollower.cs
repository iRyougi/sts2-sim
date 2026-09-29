namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class KinFollower : MonsterModel
{
    private bool _startsWithDance;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 62, 58);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 63, 59);

    public bool StartsWithDance
    {
        get => _startsWithDance;
        set
        {
            AssertMutable();
            _startsWithDance = value;
        }
    }

    private int DanceStrength => AscensionValue(AscensionLevel.DeadlyEnemies, 3, 2);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<MinionPower>(
            Creature.CombatState!,
            Creature,
            1m,
            Creature,
            cardSource: null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var quickSlash = new MoveState(
            "QUICK_SLASH_MOVE",
            QuickSlashMove,
            new SingleAttackIntent(5));
        var boomerang = new MoveState(
            "BOOMERANG_MOVE",
            BoomerangMove,
            new MultiAttackIntent(2, 2));
        var powerDance = new MoveState(
            "POWER_DANCE_MOVE",
            PowerDanceMove,
            new BuffIntent());
        quickSlash.FollowUpState = boomerang;
        boomerang.FollowUpState = powerDance;
        powerDance.FollowUpState = quickSlash;
        return new MonsterMoveStateMachine(
            new MonsterState[] { quickSlash, boomerang, powerDance },
            StartsWithDance ? powerDance : quickSlash);
    }

    private async Task QuickSlashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(5).FromMonster(this).Execute();
    }

    private async Task BoomerangMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(2).WithHitCount(2).FromMonster(this).Execute();
    }

    private async Task PowerDanceMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            DanceStrength,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
