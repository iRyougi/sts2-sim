namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>偏离 #182：真实源码在每个招式后驱动一个 <c>vantom_progress</c> 自定义音乐参数递增，
/// 代表 Boss 逐渐"膨胀变形"的纯演出效果，不影响任何战斗数值，本项目无音频/演出层，不移植。</summary>
public sealed class Vantom : MonsterModel
{
    private const int WoundCount = 3;
    private const int PrepareStrength = 2;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 183, 173);

    public override int MaxInitialHp => MinInitialHp;

    private int SlipperyAmount => AscensionValue(AscensionLevel.ToughEnemies, 9, 8);

    private int InkBlotDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 8, 7);

    private int InkyLanceDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 7, 6);

    private int DismemberDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 30, 26);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SlipperyPower>(
            Creature.CombatState!,
            Creature,
            SlipperyAmount,
            Creature,
            cardSource: null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var inkBlot = new MoveState(
            "INK_BLOT_MOVE",
            InkBlotMove,
            new SingleAttackIntent(InkBlotDamage));
        var inkyLance = new MoveState(
            "INKY_LANCE_MOVE",
            InkyLanceMove,
            new MultiAttackIntent(InkyLanceDamage, 2));
        var dismember = new MoveState(
            "DISMEMBER_MOVE",
            DismemberMove,
            new SingleAttackIntent(DismemberDamage),
            new StatusIntent(WoundCount));
        var prepare = new MoveState(
            "PREPARE_MOVE",
            PrepareMove,
            new BuffIntent());
        inkBlot.FollowUpState = inkyLance;
        inkyLance.FollowUpState = dismember;
        dismember.FollowUpState = prepare;
        prepare.FollowUpState = inkBlot;
        return new MonsterMoveStateMachine(
            new MonsterState[] { inkBlot, inkyLance, dismember, prepare },
            inkBlot);
    }

    private async Task InkBlotMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(InkBlotDamage).FromMonster(this).Execute();
    }

    private async Task InkyLanceMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(InkyLanceDamage).WithHitCount(2).FromMonster(this).Execute();
    }

    private async Task DismemberMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(DismemberDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            Player owner = (target.Player ?? target.PetOwner)!;
            if (owner.Creature.IsDead)
                continue;

            for (int index = 0; index < WoundCount; index++)
            {
                var wound = (Wound)ModelDb.Card<Wound>().MutableClone();
                wound.AssignOwner(owner);
                await CardPileCmd.Generate(Creature.CombatState!, wound, PileType.Discard, creator: null);
            }
        }
    }

    private async Task PrepareMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            PrepareStrength,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
