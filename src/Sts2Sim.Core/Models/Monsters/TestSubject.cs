namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>
/// Test Subject's three-form resurrection boss lifecycle.
/// 偏离 #279：省略 SaveManager.Progress/RunState.ExtraFields 的击杀进度；真实字段只驱动标题文案与音乐参数。
/// 偏离 #280：省略 Godot GenerateAnimator 与形态切换动画；战斗状态机和每次复活时机完整保留。
/// 偏离 #281：省略 Test Subject 的 Sfx/Vfx/SetColor 及 Wither FakeUpgrade 的立绘/标题切换；数值与玩法完整保留。
/// 偏离 #282：本仓库没有 Creature.ScaleHpForMultiplayer；复活直接使用 100/200/300（A8: 111/212/313）权威 HP。
/// </summary>
public sealed class TestSubject : MonsterModel
{
    private MoveState _deadState = null!;
    private int _respawns;
    private int _extraMultiClawCount;

    private MoveState DeadState
    {
        get => _deadState;
        set
        {
            AssertMutable();
            _deadState = value;
        }
    }

    private int Respawns
    {
        get => _respawns;
        set
        {
            AssertMutable();
            _respawns = value;
        }
    }

    private int ExtraMultiClawCount
    {
        get => _extraMultiClawCount;
        set
        {
            AssertMutable();
            _extraMultiClawCount = value;
        }
    }

    public int FirstFormHp => Ascension(AscensionLevel.ToughEnemies, 111, 100);
    public int SecondFormHp => Ascension(AscensionLevel.ToughEnemies, 212, 200);
    public int ThirdFormHp => Ascension(AscensionLevel.ToughEnemies, 313, 300);

    public override int MinInitialHp => FirstFormHp;
    public override int MaxInitialHp => MinInitialHp;

    private int EnrageAmount => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);
    private int BiteDamage => Ascension(AscensionLevel.DeadlyEnemies, 22, 20);
    private int SkullBashDamage => Ascension(AscensionLevel.DeadlyEnemies, 16, 14);
    private int MultiClawDamage => Ascension(AscensionLevel.DeadlyEnemies, 11, 10);
    private int MultiClawTotalCount => 3 + ExtraMultiClawCount;
    private int Phase3LacerateDamage => Ascension(AscensionLevel.DeadlyEnemies, 11, 10);
    private const int BigPounceDamage = 45;
    private int BurningGrowlBurnCount => Ascension(AscensionLevel.DeadlyEnemies, 5, 3);
    private int BurningGrowlStrengthGain => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);

    public override bool ShouldDisappearFromDoom() => Respawns >= 2;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<AdaptablePower>(Creature.CombatState!, Creature, 1m, Creature, null);
        await PowerCmd.Apply<EnragePower>(Creature.CombatState!, Creature, EnrageAmount, Creature, null);
    }

    public Task TriggerDeadState()
    {
        SetMoveImmediate(DeadState, forceTransition: true);
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();
        DeadState = new MoveState("RESPAWN_MOVE", RespawnMove, new HealIntent(), new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true,
        };
        var bite = new MoveState("BITE_MOVE", BiteMove, new SingleAttackIntent(BiteDamage));
        var skullBash = new MoveState(
            "SKULL_BASH_MOVE",
            SkullBashMove,
            new SingleAttackIntent(SkullBashDamage),
            new DebuffIntent());
        var multiClaw = new MoveState(
            "MULTI_CLAW_MOVE",
            MultiClawMove,
            new MultiAttackIntent(() => MultiClawDamage, () => MultiClawTotalCount));
        var lacerate = new MoveState(
            "PHASE3_LACERATE_MOVE",
            Phase3LacerateMove,
            new MultiAttackIntent(Phase3LacerateDamage, 3));
        var bigPounce = new MoveState("BIG_POUNCE", BigPounceMove, new SingleAttackIntent(BigPounceDamage));
        var burningGrowl = new MoveState(
            "BURNING_GROWL_MOVE",
            BurningGrowlMove,
            new StatusIntent(BurningGrowlBurnCount),
            new BuffIntent());
        var reviveBranch = new ConditionalBranchState("REVIVE_BRANCH")
            .AddBranch(_ => Respawns < 2, "MULTI_CLAW_MOVE")
            .AddBranch(_ => Respawns >= 2, "PHASE3_LACERATE_MOVE");

        bite.FollowUpState = skullBash;
        skullBash.FollowUpState = bite;
        multiClaw.FollowUpState = multiClaw;
        lacerate.FollowUpState = bigPounce;
        bigPounce.FollowUpState = burningGrowl;
        burningGrowl.FollowUpState = lacerate;
        DeadState.FollowUpState = reviveBranch;

        states.Add(DeadState);
        states.Add(bite);
        states.Add(skullBash);
        states.Add(multiClaw);
        states.Add(lacerate);
        states.Add(bigPounce);
        states.Add(burningGrowl);
        states.Add(reviveBranch);
        return new MonsterMoveStateMachine(states, bite);
    }

    private async Task RespawnMove(IReadOnlyList<Creature> targets)
    {
        Respawns++;
        Creature.GetPower<AdaptablePower>()?.DoRevive();
        switch (Respawns)
        {
            case 1:
                await Revive(SecondFormHp);
                await PowerCmd.Apply<PainfulStabsPower>(Creature.CombatState!, Creature, 1m, Creature, null);
                break;
            case 2:
                await Revive(ThirdFormHp);
                await PowerCmd.Apply<NemesisPower>(Creature.CombatState!, Creature, 1m, Creature, null);
                if (Creature.GetPower<AdaptablePower>() is { } adaptable)
                {
                    await PowerCmd.Remove(adaptable);
                }
                if (Creature.GetPower<PainfulStabsPower>() is { } painfulStabs)
                {
                    await PowerCmd.Remove(painfulStabs);
                }
                break;
        }
    }

    private Task BiteMove(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(BiteDamage).FromMonster(this).Execute();

    private async Task SkullBashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SkullBashDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private async Task MultiClawMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(MultiClawDamage).WithHitCount(MultiClawTotalCount).FromMonster(this).Execute();
        ExtraMultiClawCount++;
    }

    private Task Phase3LacerateMove(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(Phase3LacerateDamage).WithHitCount(3).FromMonster(this).Execute();

    private Task BigPounceMove(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(BigPounceDamage).FromMonster(this).Execute();

    private async Task BurningGrowlMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            for (int index = 0; index < BurningGrowlBurnCount; index++)
            {
                var burn = (Burn)ModelDb.Card<Burn>().MutableClone();
                burn.AssignOwner(target.Player!);
                await CardPileCmd.Generate(Creature.CombatState!, burn, PileType.Discard, creator: null);
            }
        }
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!, Creature, BurningGrowlStrengthGain, Creature, null);
    }

    private async Task Revive(int baseRespawnHp)
    {
        AssertMutable();
        decimal scaledHp = baseRespawnHp;
        await CreatureCmd.SetMaxHp(Creature, scaledHp);
        await CreatureCmd.Heal(Creature, scaledHp);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_respawns);
        builder.Append(_extraMultiClawCount);
    }
}
