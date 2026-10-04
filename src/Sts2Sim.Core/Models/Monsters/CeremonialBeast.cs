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

    private bool _isStunnedByPlowRemoval;
    private bool _isInSecondPhase;
    private MoveState _beastCryState = null!;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 262, 252);

    public override int MaxInitialHp => MinInitialHp;

    private bool IsStunnedByPlowRemoval
    {
        get => _isStunnedByPlowRemoval;
        set
        {
            AssertMutable();
            _isStunnedByPlowRemoval = value;
        }
    }

    public bool IsInSecondPhase
    {
        get => _isInSecondPhase;
        private set
        {
            AssertMutable();
            _isInSecondPhase = value;
        }
    }

    public MoveState BeastCryState
    {
        get => _beastCryState;
        set
        {
            AssertMutable();
            _beastCryState = value;
        }
    }

    private int PlowAmount => AscensionValue(AscensionLevel.DeadlyEnemies, 160, 150);

    private int PlowDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 20, 18);

    private int StompDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 17, 15);

    private int CrushDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 19, 17);

    private int CrushStrength => AscensionValue(AscensionLevel.DeadlyEnemies, 4, 3);

    public Task SetStunned()
    {
        IsStunnedByPlowRemoval = true;
        IsInSecondPhase = true;
        return Task.CompletedTask;
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
        BeastCryState = new MoveState(
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
        stun.FollowUpState = BeastCryState;
        BeastCryState.FollowUpState = stomp;
        stomp.FollowUpState = crush;
        crush.FollowUpState = BeastCryState;
        return new MonsterMoveStateMachine(
            new MonsterState[] { plow, stamp, stun, BeastCryState, stomp, crush },
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

    public Task StunnedMove(IReadOnlyList<Creature> targets)
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

    internal override void RestoreCombatCloneReferencesFrom(
        MonsterModel source,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        var beast = (CeremonialBeast)source;
        if (beast._beastCryState is not { } savedState)
        {
            _beastCryState = null!;
            return;
        }

        if (beast.MoveStateMachine is null ||
            !beast.MoveStateMachine.States.TryGetValue(savedState.StateId, out MonsterState? sourceState) ||
            !ReferenceEquals(sourceState, savedState) ||
            MoveStateMachine is null ||
            !MoveStateMachine.States.TryGetValue(savedState.StateId, out MonsterState? clonedState) ||
            clonedState is not MoveState clonedMove)
        {
            throw new InvalidOperationException(
                $"CeremonialBeast saved move {savedState.StateId} is not present in its combat state graph.");
        }

        BeastCryState = clonedMove;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_beastCryState is not null);
        if (_beastCryState is { } savedState)
        {
            builder.Append(savedState.StateId);
        }
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
