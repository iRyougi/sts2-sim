using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Commands.Builders;

/// <summary>
/// 攻击构建器。偏离 #30：删掉全部 VFX/SFX/动画链式方法（`WithAttackerAnim`/`WithHitFx`/`OnlyPlayAnimOnce` 等）,
/// 只保留影响结算逻辑的部分；Hook 调用序与随机目标抽取逐字保留。
/// </summary>
public sealed class AttackCommand
{
    private enum SourceType
    {
        None,
        Card,
        Monster,
    }

    private readonly decimal _damagePerHit;
    private readonly List<List<DamageResult>> _results = new();
    private int _hitCount = 1;
    private SourceType _sourceType;
    private ICombatState? _combatState;
    private Creature? _singleTarget;
    private bool _allowDuplicateRandomTargets = true;

    public AttackCommand(decimal damagePerHit)
    {
        _damagePerHit = damagePerHit;
    }

    public Creature? Attacker { get; private set; }

    public AbstractModel? ModelSource { get; private set; }

    public CardPlay? CardPlay { get; private set; }

    public CombatSide TargetSide { get; private set; }

    public ValueProp DamageProps { get; private set; } = ValueProp.Move;

    public bool IsSingleTargeted => _singleTarget != null;

    public bool IsMultiTargeted => _combatState != null;

    public bool IsRandomlyTargeted { get; private set; }

    public IEnumerable<List<DamageResult>> Results => _results;

    public static Task<AttackContext> CreateContextAsync(ICombatState combatState, CardPlay cardPlay) =>
        AttackContext.CreateAsync(combatState, cardPlay);

    public AttackCommand FromCard(CardModel card, CardPlay? cardPlay)
    {
        if (Attacker != null)
        {
            throw new InvalidOperationException("Attacker has already been set.");
        }

        Attacker = card.Owner.Creature;
        ModelSource = card;
        CardPlay = cardPlay;
        _sourceType = SourceType.Card;
        return this;
    }

    public AttackCommand FromMonster(MonsterModel monster)
    {
        if (Attacker != null)
        {
            throw new InvalidOperationException("Attacker has already been set.");
        }

        Attacker = monster.Creature;
        _sourceType = SourceType.Monster;
        return TargetingAllOpponents(monster.Creature.CombatState!);
    }

    public AttackCommand Targeting(Creature target)
    {
        if (_singleTarget != null || _combatState != null)
        {
            throw new InvalidOperationException("Targets already set.");
        }

        _singleTarget = target;
        TargetSide = target.Side;
        return this;
    }

    public AttackCommand TargetingAllOpponents(ICombatState combatState)
    {
        if (_singleTarget != null || _combatState != null)
        {
            throw new InvalidOperationException("Targets already set.");
        }

        if (Attacker == null)
        {
            throw new InvalidOperationException("We require an attacker to be able to grab its opponents.");
        }

        _combatState = combatState;
        TargetSide = Attacker.Side == CombatSide.Enemy ? CombatSide.Player : CombatSide.Enemy;
        return this;
    }

    public AttackCommand TargetingRandomOpponents(ICombatState combatState, bool allowDuplicates = true)
    {
        if (_singleTarget != null || _combatState != null)
        {
            throw new InvalidOperationException("Targets already set.");
        }

        if (Attacker == null)
        {
            throw new InvalidOperationException("We require an attacker to be able to grab its opponents.");
        }

        _combatState = combatState;
        IsRandomlyTargeted = true;
        _allowDuplicateRandomTargets = allowDuplicates;
        return this;
    }

    public AttackCommand Unpowered()
    {
        DamageProps |= ValueProp.Unpowered;
        return this;
    }

    public AttackCommand WithHitCount(int hitCount)
    {
        _hitCount = hitCount;
        return this;
    }

    /// <summary>执行攻击。逐字移植调用序：Hook.BeforeAttack → 折叠命中次数 → 逐次结算 → Hook.AfterAttack。</summary>
    public async Task<AttackCommand> Execute()
    {
        if (Attacker == null)
        {
            throw new InvalidOperationException("No attacker set.");
        }

        ICombatState combatState = Attacker.CombatState ?? throw new InvalidOperationException("Attacker has no combat state.");
        if (Attacker.IsDead)
        {
            return this;
        }

        if (!IsSingleTargeted && !IsMultiTargeted)
        {
            throw new InvalidOperationException("No targets set, a Targeting method must be called before Execute.");
        }

        await Hook.BeforeAttack(combatState, this);
        int attackCount = Hook.ModifyAttackHitCount(combatState, this, _hitCount);
        for (int i = 0; i < attackCount; i++)
        {
            if (Attacker.IsDead)
            {
                break;
            }

            List<Creature> validTargets = GetPossibleTargets().Where(c => c.IsAlive).ToList();
            if (validTargets.Count == 0)
            {
                break;
            }

            Creature? singleTarget = GetSingleTarget(validTargets, combatState);
            IEnumerable<Creature> hitTargets = singleTarget != null ? new[] { singleTarget } : validTargets;
            IReadOnlyList<DamageResult> hitResults = await Commands.CreatureCmd.Damage(
                combatState,
                hitTargets,
                _damagePerHit,
                DamageProps,
                Attacker,
                ModelSource as CardModel,
                CardPlay);
            _results.Add(hitResults.ToList());
        }

        await Hook.AfterAttack(combatState, this);
        return this;
    }

    internal void IncrementHitsInternal() => _hitCount++;

    internal void AddResultsInternal(IEnumerable<DamageResult> results)
    {
        _results.Add(results.ToList());
    }

    private IReadOnlyList<Creature> GetPossibleTargets()
    {
        if (IsSingleTargeted)
        {
            return new[] { _singleTarget! };
        }

        if (IsMultiTargeted)
        {
            if (_sourceType == SourceType.Monster)
            {
                return _combatState!.PlayerCreatures;
            }

            if (Attacker == null)
            {
                throw new InvalidOperationException("We require an attacker to be able to grab its opponents.");
            }

            return _combatState!.GetOpponentsOf(Attacker);
        }

        throw new InvalidOperationException("No targets set, a Targeting method must be called before Execute.");
    }

    private Creature? GetSingleTarget(List<Creature> validTargets, ICombatState combatState)
    {
        if (!IsRandomlyTargeted)
        {
            return validTargets.Count == 1 ? validTargets[0] : null;
        }

        if (!_allowDuplicateRandomTargets)
        {
            HashSet<Creature> alreadyHit = _results.SelectMany(r => r).Select(r => r.Receiver).ToHashSet();
            validTargets = validTargets.Where(c => !alreadyHit.Contains(c)).ToList();
            if (validTargets.Count == 0)
            {
                throw new InvalidOperationException("No valid targets for attack with duplicates disallowed.");
            }
        }

        return combatState.RunState.Rng.CombatTargets.NextItem(validTargets);
    }
}
