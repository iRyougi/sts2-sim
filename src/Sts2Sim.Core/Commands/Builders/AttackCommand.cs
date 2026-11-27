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

    /// <summary>原版 <c>AttackCommand.FromOsty</c>：由 Osty 发起、来源仍是这张卡的攻击。</summary>
    public AttackCommand FromOsty(Creature osty, CardModel card, CardPlay? cardPlay)
    {
        if (osty.Monster is not Models.Monsters.Osty)
        {
            throw new ArgumentException("Creature is not Osty.", nameof(osty));
        }

        Attacker = osty;
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

    public AttackCommand FromArrow()
    {
        DamageProps |= ValueProp.FromArrow;
        return this;
    }

    public AttackCommand WithHitCount(int hitCount)
    {
        _hitCount = hitCount;
        return this;
    }

    /// <summary>执行攻击。逐字移植 v0.114.0：攻击期间置 <see cref="Creature.IsAttacking"/>（try/finally 恢复原值）；
    /// 攻击者在本次攻击中死亡且不在怪物行动中时，攻击结束后统一收尾——可移除的敌人移出战斗，按死亡规则移除能力并触发
    /// <c>AfterRemoved</c>。原版此处还有 <c>Player.DeactivateHooks()</c>，模拟器没有停用玩家钩子的机制（Kill 也不调），不加。</summary>
    public async Task<AttackCommand> Execute()
    {
        Creature? attacker = Attacker;
        bool wasAttacking = attacker?.IsAttacking ?? false;
        bool attackerWasDead = attacker?.IsDead ?? false;
        if (attacker != null)
        {
            attacker.IsAttacking = true;
        }

        try
        {
            await ExecuteInternal();
        }
        finally
        {
            if (attacker != null)
            {
                attacker.IsAttacking = wasAttacking;
            }
        }

        if (attacker != null && attacker.IsDead && !attackerWasDead && !(attacker.Monster?.IsPerformingMove ?? false))
        {
            ICombatState? combatState = attacker.CombatState;
            if (combatState != null && Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, attacker) &&
                attacker.Side == CombatSide.Enemy && combatState.Enemies.Contains(attacker))
            {
                combatState.RemoveCreature(attacker);
            }

            // 原版先移出战斗再 attacker.RemoveAllPowersAfterDeath()：已移出的攻击者不再咨询监听者。
            await Commands.CreatureCmd.RemoveAllPowersAfterDeath(attacker);
        }

        return this;
    }

    /// <summary>逐字移植调用序：Hook.BeforeAttack → 折叠命中次数 → 逐次结算 → Hook.AfterAttack。</summary>
    private async Task ExecuteInternal()
    {
        if (Attacker == null)
        {
            throw new InvalidOperationException("No attacker set.");
        }

        ICombatState combatState = Attacker.CombatState ?? throw new InvalidOperationException("Attacker has no combat state.");
        if (combatState.IsOverOrEnding() || Attacker.IsDead)
        {
            return;
        }

        if (!IsSingleTargeted && !IsMultiTargeted)
        {
            throw new InvalidOperationException("No targets set, a Targeting method must be called before Execute.");
        }

        await Hook.BeforeAttack(combatState, this);
        int attackCount = Hook.ModifyAttackHitCount(combatState, this, _hitCount);
        Creature[]? singleTargetArray = null;
        List<Creature>? validTargets = null;
        for (int i = 0; i < attackCount; i++)
        {
            if (Attacker.IsDead)
            {
                break;
            }

            IEnumerable<Creature> hitTargets;
            if (IsSingleTargeted)
            {
                Creature target = _singleTarget!;
                if (!target.IsAlive)
                {
                    break;
                }
                singleTargetArray ??= new Creature[1];
                singleTargetArray[0] = target;
                hitTargets = singleTargetArray;
            }
            else
            {
                IReadOnlyList<Creature> possible = GetPossibleTargets();
                if (validTargets is null) validTargets = new List<Creature>(possible.Count);
                else validTargets.Clear();
                for (int p = 0; p < possible.Count; p++)
                {
                    Creature candidate = possible[p];
                    if (candidate.IsAlive) validTargets.Add(candidate);
                }
                if (validTargets.Count == 0)
                {
                    break;
                }

                Creature? singleTarget = GetSingleTarget(validTargets, combatState);
                if (singleTarget != null)
                {
                    singleTargetArray ??= new Creature[1];
                    singleTargetArray[0] = singleTarget;
                    hitTargets = singleTargetArray;
                }
                else
                {
                    hitTargets = validTargets;
                }
            }

            IReadOnlyList<DamageResult> hitResults = await Commands.CreatureCmd.Damage(
                combatState,
                hitTargets,
                _damagePerHit,
                DamageProps,
                Attacker,
                ModelSource as CardModel,
                CardPlay);
            _results.Add(hitResults is List<DamageResult> ownedList ? new List<DamageResult>(ownedList) : hitResults.ToList());
        }

        // 原版在 AfterAttack 之前记录 CreatureAttackedEntry；玩法上只有 Osty 的攻击被读取。
        if (Attacker?.Monster is Models.Monsters.Osty && Attacker.PetOwner is { } ostyOwner &&
            combatState is CombatState concreteState)
            concreteState.SemanticHistory.Record(concreteState, CombatSemanticHistory.ActorEvent.OstyAttack, ostyOwner);

        await Hook.AfterAttack(combatState, this);
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
