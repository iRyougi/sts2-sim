namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>偏离 #183：真实源码里 <c>KinPriest</c> 监听队友死亡，最后一只 <c>KinFollower</c> 阵亡时播放
/// "followersDeathLine" 台词特效（纯演出，不改变数值/状态机），本项目无台词/音效层，不移植这条监听。</summary>
public sealed class KinPriest : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 199, 190);

    public override int MaxInitialHp => MinInitialHp;

    private int OrbDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 9, 8);

    private int RitualStrength => AscensionValue(AscensionLevel.DeadlyEnemies, 3, 2);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var orbOfFrailty = new MoveState(
            "ORB_OF_FRAILTY_MOVE",
            OrbOfFrailtyMove,
            new SingleAttackIntent(OrbDamage),
            new DebuffIntent());
        var orbOfWeakness = new MoveState(
            "ORB_OF_WEAKNESS_MOVE",
            OrbOfWeaknessMove,
            new SingleAttackIntent(OrbDamage),
            new DebuffIntent());
        var beam = new MoveState(
            "BEAM_MOVE",
            BeamMove,
            new MultiAttackIntent(3, 3));
        var ritual = new MoveState(
            "RITUAL_MOVE",
            RitualMove,
            new BuffIntent());
        orbOfFrailty.FollowUpState = orbOfWeakness;
        orbOfWeakness.FollowUpState = beam;
        beam.FollowUpState = ritual;
        ritual.FollowUpState = orbOfFrailty;
        return new MonsterMoveStateMachine(
            new MonsterState[] { orbOfFrailty, orbOfWeakness, beam, ritual },
            orbOfFrailty);
    }

    private async Task OrbOfFrailtyMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(OrbDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<FrailPower>(
                Creature.CombatState!,
                target,
                1m,
                Creature,
                cardSource: null);
        }
    }

    private async Task OrbOfWeaknessMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(OrbDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(
                Creature.CombatState!,
                target,
                1m,
                Creature,
                cardSource: null);
        }
    }

    private async Task BeamMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(3).WithHitCount(3).FromMonster(this).Execute();
    }

    private async Task RitualMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            RitualStrength,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
