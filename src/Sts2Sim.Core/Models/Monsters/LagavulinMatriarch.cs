namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.ValueProps;

public sealed class LagavulinMatriarch : MonsterModel
{
    public const string SlashMoveId = "SLASH_MOVE";

    private bool _isAwake;
    private bool _isShellAwake;

    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 233, 222);

    public override int MaxInitialHp => MinInitialHp;

    private int SlashDamage => Value(AscensionLevel.DeadlyEnemies, 21, 19);

    private int Slash2Damage => Value(AscensionLevel.DeadlyEnemies, 14, 12);

    private int Slash2Block => Value(AscensionLevel.ToughEnemies, 14, 12);

    private int DisembowelDamage => Value(AscensionLevel.DeadlyEnemies, 10, 9);

    private const int DisembowelRepeat = 2;

    public bool IsAwake
    {
        get => _isAwake;
        set
        {
            AssertMutable();
            _isAwake = value;
        }
    }

    public bool IsShellAwake
    {
        get => _isShellAwake;
        set
        {
            AssertMutable();
            _isShellAwake = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        IsAwake = false;
        await PowerCmd.Apply<PlatingPower>(Creature.CombatState!, Creature, 12m, Creature, null);
        await PowerCmd.Apply<AsleepPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    public override Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Creature &&
            Creature.CurrentHp <= Creature.MaxHp / 2 &&
            !IsShellAwake)
        {
            IsShellAwake = true;
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var sleep = new MoveState("SLEEP_MOVE", SleepMove, new SleepIntent());
        var slash = new MoveState(SlashMoveId, SlashMove, new SingleAttackIntent(() => SlashDamage));
        var slash2 = new MoveState(
            "SLASH2_MOVE",
            Slash2Move,
            new SingleAttackIntent(() => Slash2Damage),
            new DefendIntent());
        var disembowel = new MoveState(
            "DISEMBOWEL_MOVE",
            DisembowelMove,
            new MultiAttackIntent(() => DisembowelDamage, () => DisembowelRepeat));
        var soulSiphon = new MoveState(
            "SOUL_SIPHON_MOVE",
            SoulSiphonMove,
            new DebuffIntent(),
            new BuffIntent());
        var branch = new ConditionalBranchState("SLEEP_BRANCH")
            .AddBranch(creature => creature.HasPower<AsleepPower>(), sleep.Id)
            .AddBranch(_ => true, slash.Id);

        sleep.FollowUpState = branch;
        slash.FollowUpState = disembowel;
        disembowel.FollowUpState = slash2;
        slash2.FollowUpState = soulSiphon;
        soulSiphon.FollowUpState = slash;

        return new MonsterMoveStateMachine(
            [branch, sleep, slash, slash2, soulSiphon, disembowel],
            sleep);
    }

    private Task SleepMove(IReadOnlyList<Creature> _) => Task.CompletedTask;

    public Task WakeUpMove(IReadOnlyList<Creature> _)
    {
        if (!IsAwake)
        {
            IsAwake = true;
        }

        return Task.CompletedTask;
    }

    internal void SetWakeUpStunned() =>
        SetMoveImmediate(CreateWakeUpStunnedMove(), forceTransition: true);

    private MoveState CreateWakeUpStunnedMove() =>
        new("STUNNED", _ => WakeUpMove(Array.Empty<Creature>()), new StunIntent())
        {
            FollowUpStateId = SlashMoveId,
            MustPerformOnceBeforeTransitioning = true,
            CombatCloneFactory = cloned =>
                ((LagavulinMatriarch)cloned).CreateWakeUpStunnedMove(),
        };

    private Task SlashMove(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(SlashDamage).FromMonster(this).Execute();

    private async Task Slash2Move(IReadOnlyList<Creature> _)
    {
        await DamageCmd.Attack(Slash2Damage).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(
            Creature.CombatState!,
            Creature,
            Slash2Block,
            ValueProp.Move,
            null,
            null);
    }

    private Task DisembowelMove(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(DisembowelDamage)
            .WithHitCount(DisembowelRepeat)
            .FromMonster(this)
            .Execute();

    private async Task SoulSiphonMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<StrengthPower>(
                Creature.CombatState!,
                target,
                -2m,
                Creature,
                null);
            await PowerCmd.Apply<DexterityPower>(
                Creature.CombatState!,
                target,
                -2m,
                Creature,
                null);
        }

        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            2m,
            Creature,
            null);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_isAwake);
        builder.Append(_isShellAwake);
    }

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
