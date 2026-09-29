namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class CeremonialBeast : MonsterModel
{
    private const int PlowStrength = 2;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 262, 252);

    public override int MaxInitialHp => MinInitialHp;

    public bool IsStunnedByPlowRemoval { get; private set; }

    public bool IsInSecondPhase { get; private set; }

    private int PlowAmount => AscensionValue(AscensionLevel.DeadlyEnemies, 160, 150);

    private int PlowDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 20, 18);

    private int StompDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 17, 15);

    private int CrushDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 19, 17);

    private int CrushStrength => AscensionValue(AscensionLevel.DeadlyEnemies, 4, 3);

    public void SetStunned()
    {
        AssertMutable();
        if (Creature.CombatState is null || Creature.IsDead)
        {
            return;
        }

        IsStunnedByPlowRemoval = true;
        IsInSecondPhase = true;
        MoveState stun = (MoveState)MoveStateMachine!.States["STUN_MOVE"];
        SetMoveImmediate(stun, forceTransition: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var stamp = new MoveState(
            "STAMP_MOVE",
            StampMove,
            new BuffIntent());
        var plow = new MoveState(
            "PLOW_MOVE",
            PlowMove,
            new SingleAttackIntent(PlowDamage),
            new BuffIntent());
        var stun = new MoveState(
            "STUN_MOVE",
            StunnedMove,
            new StunIntent())
        {
            MustPerformOnceBeforeTransitioning = true,
        };
        var beastCry = new MoveState(
            "BEAST_CRY_MOVE",
            BeastCryMove,
            new DebuffIntent());
        var stomp = new MoveState(
            "STOMP_MOVE",
            StompMove,
            new SingleAttackIntent(StompDamage));
        var crush = new MoveState(
            "CRUSH_MOVE",
            CrushMove,
            new SingleAttackIntent(CrushDamage),
            new BuffIntent());
        stamp.FollowUpState = plow;
        plow.FollowUpState = plow;
        stun.FollowUpState = beastCry;
        beastCry.FollowUpState = stomp;
        stomp.FollowUpState = crush;
        crush.FollowUpState = beastCry;
        return new MonsterMoveStateMachine(
            new MonsterState[] { stamp, plow, stun, beastCry, stomp, crush },
            stamp);
    }

    private async Task StampMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<PlowPower>(
            Creature.CombatState!,
            Creature,
            PlowAmount,
            Creature,
            cardSource: null);
    }

    private async Task PlowMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PlowDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            PlowStrength,
            Creature,
            cardSource: null);
    }

    private Task StunnedMove(IReadOnlyList<Creature> targets)
    {
        IsStunnedByPlowRemoval = false;
        return Task.CompletedTask;
    }

    private async Task BeastCryMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<RingingPower>(
                Creature.CombatState!,
                target,
                1m,
                Creature,
                cardSource: null);
        }
    }

    private async Task StompMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StompDamage).FromMonster(this).Execute();
    }

    private async Task CrushMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(CrushDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            CrushStrength,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
