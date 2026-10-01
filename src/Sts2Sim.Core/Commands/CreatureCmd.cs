using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Commands;

/// <summary>
/// 生物级伤害/格挡结算命令。逐字移植调用序（<c>MegaCrit.Sts2.Core.Commands.CreatureCmd</c>），
/// 包括承伤的 BeforeOsty / AfterOsty 两阶段与 Osty 的未格挡伤害重定向。
/// </summary>
public static class CreatureCmd
{
    public static Task Stun(Creature creature, string? nextMoveId = null) =>
        StunCore(creature, static (_, _) => Task.CompletedTask, nextMoveId);

    public static Task Stun<TMonster>(
        Creature creature,
        Func<TMonster, IReadOnlyList<Creature>, Task> stunMove,
        string? nextMoveId = null)
        where TMonster : MonsterModel
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(stunMove);
        MonsterModel monster = creature.Monster
            ?? throw new InvalidOperationException("Can't stun a player.");
        if (monster is not TMonster)
        {
            throw new InvalidOperationException($"Stun callback requires a {typeof(TMonster).Name} monster.");
        }

        return StunCore(
            creature,
            (monster, targets) => stunMove((TMonster)monster, targets),
            nextMoveId);
    }

    private static Task StunCore(
        Creature creature,
        Func<MonsterModel, IReadOnlyList<Creature>, Task> stunMove,
        string? nextMoveId)
    {
        ArgumentNullException.ThrowIfNull(creature);
        MonsterModel monster = creature.Monster
            ?? throw new InvalidOperationException("Can't stun a player.");
        if (creature.CombatState is null || creature.IsDead)
        {
            return Task.CompletedTask;
        }

        nextMoveId = string.IsNullOrEmpty(nextMoveId)
            ? monster.MoveStateMachine!.StateLog.Last().Id
            : nextMoveId;
        monster.SetMoveImmediate(CreateStunnedMove(monster, nextMoveId, stunMove));
        return Task.CompletedTask;
    }

    private static MoveState CreateStunnedMove(
        MonsterModel owner,
        string nextMoveId,
        Func<MonsterModel, IReadOnlyList<Creature>, Task> stunMove)
    {
        return new MoveState("STUNNED", targets => stunMove(owner, targets), new StunIntent())
        {
            FollowUpStateId = nextMoveId,
            MustPerformOnceBeforeTransitioning = true,
            CombatCloneFactory = cloned => CreateStunnedMove(cloned, nextMoveId, stunMove),
        };
    }

    /// <summary>Adds and initializes a monster during an active combat without replaying combat-start hooks.</summary>
    public static async Task<Creature> Add(
        MonsterModel monster,
        ICombatState combatState,
        CombatSide side,
        string? slotName)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(combatState);
        Creature creature = combatState.CreateCreature(monster, side, slotName);
        await Add(creature);
        return creature;
    }

    /// <summary>Adds a creature that was already created and attached to its combat state.</summary>
    public static async Task Add(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ICombatState combatState = creature.CombatState
            ?? throw new InvalidOperationException("Attempted to add a creature with no combat state.");
        if (!combatState.IsLiveCombat())
        {
            throw new InvalidOperationException("Monsters can only be added to a live combat.");
        }

        MonsterModel monster = creature.Monster
            ?? throw new InvalidOperationException("Only monster-backed creatures can be added dynamically.");
        combatState.AddCreature(creature);
        monster.SetUpForCombat();
        // CombatManager.AddCreature sorts immediately after setup, before room hooks.
        if (creature.SlotName is not null && combatState is CombatState state)
            state.SortEnemiesBySlotName();
        if (creature.Side == CombatSide.Enemy)
        {
            await monster.AfterAddedToRoom();
        }
        // CombatManager.AfterCreatureAdded only rolls a newly added enemy during the player's side.
        // Player-side pets keep their no-op state machine without consuming MonsterAi.
        if (creature.Side == CombatSide.Enemy && combatState.CurrentSide == CombatSide.Player)
        {
            monster.RollMove(combatState.PlayerCreatures);
        }

        await Hook.AfterCreatureAddedToCombat(combatState, creature);
    }

    public static async Task<IReadOnlyList<DamageResult>> Damage(
        ICombatState combatState,
        IEnumerable<Creature> targets,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        List<Creature> targetList = targets.ToList();
        var pending = new List<(DamageResult Result, bool Prevented)>();

        // One hit applies damage to every target before any result hook or death changes listeners.
        foreach (Creature target in targetList)
        {
            if (target.IsDead)
                continue;

            pending.AddRange(await DamageSingle(combatState, target, amount, props, dealer, cardSource, cardPlay));
        }

        var killed = new List<DamageResult>();
        foreach (var (result, prevented) in pending)
        {
            if (prevented)
                continue;

            Creature target = result.Receiver;
            if (result.WasBlockBroken)
                await Hook.AfterBlockBroken(combatState, target, dealer);
            if (result.UnblockedDamage > 0)
                await Hook.AfterCurrentHpChanged(combatState.RunState, combatState,
                    target, -result.UnblockedDamage);
            await Hook.AfterDamageGiven(combatState, dealer, result, props, target, cardSource);
            if (!result.WasTargetKilled || !target.IsDead)
                await Hook.AfterDamageReceived(combatState, target, result, props, dealer, cardSource);
            else
                killed.Add(result);
        }

        foreach (DamageResult result in killed)
            await ResolvePotentialDeath(combatState.RunState, combatState, result.Receiver, result);

        var results = new List<DamageResult>(pending.Count);
        foreach (var (result, _) in pending)
        {
            (combatState as CombatState)?.Observer?.DamageResolved(dealer, result);
            results.Add(result);
        }
        return results;
    }

    /// <summary>原版 <c>CreatureCmd.Damage</c> 对单个目标的结算。未格挡伤害经 BeforeOsty 阶段后由
    /// <see cref="Hook.ModifyUnblockedDamageTarget"/> 决定承受者（DieForYouPower 会把主人换成 Osty），再对承受者跑
    /// AfterOsty 阶段；若换了承受者，Osty 的溢出伤害再对原目标跑一次 AfterOsty 并扣血。结果按原版顺序返回
    /// （承受者在前），格挡量与破盾只记在原目标那一条上。</summary>
    private static async Task<IReadOnlyList<(DamageResult Result, bool Prevented)>> DamageSingle(
        ICombatState combatState,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!Hook.ShouldAllowHitting(combatState, target))
        {
            var prevented = new DamageResult(target, props);
            return [(prevented, true)];
        }

        IRunState runState = combatState.RunState;
        decimal modifiedAmount = Hook.ModifyDamage(combatState, target, dealer, amount, props, cardSource, cardPlay, out _);
        await Hook.BeforeDamageReceived(combatState, target, modifiedAmount, props, dealer, cardSource);

        Creature blockOwner = target.PetOwner?.Creature ?? target;
        decimal blockedDamage = blockOwner.DamageBlockInternal(modifiedAmount, props);
        decimal unblocked = Hook.ModifyHpLost(runState, combatState, target,
            Math.Max(modifiedAmount - blockedDamage, 0m), props, dealer, cardSource,
            HpLossHookPhase.BeforeOsty, out IEnumerable<AbstractModel> beforeOstyModifiers);
        await Hook.AfterModifyingHpLostBeforeOsty(runState, combatState, beforeOstyModifiers);

        Creature receiver = Hook.ModifyUnblockedDamageTarget(combatState, target, unblocked, props, dealer);
        unblocked = Hook.ModifyHpLost(runState, combatState, receiver, unblocked, props, dealer, cardSource,
            HpLossHookPhase.AfterOsty, out IEnumerable<AbstractModel> afterOstyModifiers);
        await Hook.AfterModifyingHpLostAfterOsty(runState, combatState, afterOstyModifiers);
        DamageResult received = receiver.LoseHpInternal(unblocked, props);
        // 原版两项都看原始目标：宠物被命中时格挡由主人承担，宠物自身格挡恒为 0；
        // 完全格挡按承受者最终扣血（转给 Osty 时是 Osty 的扣血）判断。
        bool blockBroken = target.Block <= 0 && blockedDamage > 0m;
        bool fullyBlocked = !props.HasFlag(ValueProp.Unblockable) &&
                            (blockedDamage > 0m || target.Block > 0) && (int)unblocked == 0;

        var results = new List<(DamageResult Result, bool Prevented)>(2);
        if (ReferenceEquals(receiver, target))
        {
            results.Add((received with
            {
                BlockedDamage = (int)blockedDamage, WasBlockBroken = blockBroken, WasFullyBlocked = fullyBlocked,
            }, false));
        }
        else
        {
            results.Add((received, false));
            decimal overflow = Hook.ModifyHpLost(runState, combatState, target, received.OverkillDamage, props,
                dealer, cardSource, HpLossHookPhase.AfterOsty, out IEnumerable<AbstractModel> overflowModifiers);
            await Hook.AfterModifyingHpLostAfterOsty(runState, combatState, overflowModifiers);
            DamageResult original = overflow > 0m
                ? target.LoseHpInternal(overflow, props)
                : new DamageResult(target, props);
            results.Add((original with
            {
                BlockedDamage = (int)blockedDamage, WasBlockBroken = blockBroken, WasFullyBlocked = fullyBlocked,
            }, false));
        }

        foreach (var (result, _) in results)
            (combatState as CombatState)?.DamageHistory.Record(result, result.Receiver, dealer, cardSource, combatState);
        return results;
    }

    /// <summary>Kills a combat creature directly without running the ordinary damage hook pipeline.</summary>
    public static async Task Kill(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        if (creature.IsDead)
        {
            return;
        }

        ICombatState combatState = creature.CombatState
            ?? throw new InvalidOperationException("Creature has no combat state.");
        DamageResult result = creature.LoseHpInternal(
            creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered);
        if (result.UnblockedDamage > 0)
            await Hook.AfterCurrentHpChanged(combatState.RunState, combatState,
                creature, -result.UnblockedDamage);
        await ResolvePotentialDeath(combatState.RunState, combatState, creature, result);
    }

    /// <summary>Kills a run creature while bypassing death-prevention hooks, as an explicit run-abandon action does.</summary>
    public static async Task KillUnpreventably(IRunState runState, Creature creature)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(creature);
        if (creature.IsDead)
        {
            return;
        }

        DamageResult result = creature.LoseHpInternal(
            creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered);
        if (result.UnblockedDamage > 0)
            await Hook.AfterCurrentHpChanged(runState, creature.CombatState,
                creature, -result.UnblockedDamage);
        await Hook.BeforeDeath(runState, creature.CombatState, creature);
        await ResolveConfirmedDeath(runState, creature.CombatState, creature, result);
    }

    /// <summary>Removes a living creature from combat without running the death lifecycle.</summary>
    public static Task Escape(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ICombatState? combatState = creature.CombatState;
        if (creature.IsDead || combatState is null || !combatState.IsLiveCombat() ||
            !combatState.ContainsCreature(creature))
        {
            return Task.CompletedTask;
        }

        foreach (PowerModel power in creature.Powers.ToArray())
        {
            power.RemoveInternal();
        }

        combatState.CreatureEscaped(creature);
        return Task.CompletedTask;
    }

    /// <summary>Applies run-level damage outside combat through the normal damage hook phases.</summary>
    public static async Task<DamageResult> Damage(
        IRunState runState,
        Creature target,
        decimal amount,
        ValueProp props)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(target);
        if (amount < 0m)
        {
            throw new ArgumentException("Damage cannot be negative.", nameof(amount));
        }

        decimal modifiedAmount = Hook.ModifyDamage(
            runState,
            null,
            target,
            null,
            amount,
            props,
            null,
            null,
            out _);
        await Hook.BeforeDamageReceived(runState, null, target, modifiedAmount, props, null, null);

        decimal blockedDamage = target.DamageBlockInternal(modifiedAmount, props);
        // 战斗外没有重定向：原版两个阶段依次跑在同一个目标上。
        decimal unblocked = Hook.ModifyHpLost(runState, null, target,
            Math.Max(modifiedAmount - blockedDamage, 0m), props, null, null,
            HpLossHookPhase.BeforeOsty, out IEnumerable<AbstractModel> beforeOstyModifiers);
        await Hook.AfterModifyingHpLostBeforeOsty(runState, null, beforeOstyModifiers);
        unblocked = Hook.ModifyHpLost(runState, null, target, unblocked, props, null, null,
            HpLossHookPhase.AfterOsty, out IEnumerable<AbstractModel> afterOstyModifiers);
        await Hook.AfterModifyingHpLostAfterOsty(runState, null, afterOstyModifiers);

        DamageResult result = target.LoseHpInternal(unblocked, props) with
        {
            BlockedDamage = (int)blockedDamage,
            WasBlockBroken = target.Block <= 0 && blockedDamage > 0m,
            WasFullyBlocked = !props.HasFlag(ValueProp.Unblockable) &&
                              (blockedDamage > 0m || target.Block > 0) && (int)unblocked == 0,
        };

        if (result.UnblockedDamage > 0)
            await Hook.AfterCurrentHpChanged(runState, null, target, -result.UnblockedDamage);
        await Hook.AfterDamageReceived(runState, null, target, result, props, null, null);
        return await ResolvePotentialDeath(runState, null, target, result);
    }

    public static async Task<DamageResult> LoseHp(
        IRunState runState,
        Creature target,
        decimal amount,
        ValueProp props)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(target);
        DamageResult result = target.LoseHpInternal(amount, props);
        if (result.UnblockedDamage > 0)
            await Hook.AfterCurrentHpChanged(runState, null, target, -result.UnblockedDamage);
        return await ResolvePotentialDeath(runState, null, target, result);
    }

    /// <summary>偏离 #126：新增最小的 run-level、可空 combat 防死切片，保留唯一实际 preventer，
    /// 并让战斗伤害和事件直接失血共享最终死亡结算。与原版一致，
    /// DamageResult.WasTargetKilled 保留 LoseHpInternal 对本次伤害的致死判定；
    /// 需要 Fatal 奖励的卡牌在攻击前另行检查目标的 Power。</summary>
    private static async Task<DamageResult> ResolvePotentialDeath(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        DamageResult result)
    {
        if (!result.WasTargetKilled)
        {
            return result;
        }

        for (int recursion = 0; ; recursion++)
        {
            await Hook.BeforeDeath(runState, combatState, target);
            if (Hook.ShouldDie(runState, combatState, target, out AbstractModel? preventer))
                break;
            if (recursion >= 10)
                throw new InvalidOperationException("Death prevention repeatedly left the creature dead.");
            await Hook.AfterDeath(runState, combatState, target, wasRemovalPrevented: true);
            int hpBefore = target.CurrentHp;
            await Hook.AfterPreventingDeath(preventer!, target);
            (combatState as CombatState)?.PredictionSink?.DeathPrevented(
                target, preventer!, hpBefore, target.CurrentHp);
            if (!target.IsDead)
                return result;
        }
        return await ResolveConfirmedDeath(runState, combatState, target, result);
    }

    private static async Task<DamageResult> ResolveConfirmedDeath(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        DamageResult result)
    {
        target.InvokeDiedEvent();
        bool shouldRemove = combatState is not null &&
            Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, target);
        await Hook.AfterDeath(runState, combatState, target);
        bool wasPrimaryEnemy = target.IsPrimaryEnemy;
        Creature[] livingTeammates = combatState?.GetCreaturesOnSide(target.Side)
            .Where(teammate => !ReferenceEquals(teammate, target) && teammate.IsAlive)
            .ToArray() ?? Array.Empty<Creature>();
        PowerModel[] powersToRemove = target.Powers.Where(power => combatState is null ||
            (Hook.ShouldPowerBeRemovedOnDeath(combatState, power) &&
             Hook.ShouldPowerBeRemovedAfterOwnerDeath(combatState, power))).ToArray();
        if (shouldRemove && target.Side == CombatSide.Enemy &&
            combatState!.Enemies.Contains(target) && target.Monster?.IsPerformingMove != true)
            combatState.RemoveCreature(target);
        foreach (PowerModel power in powersToRemove)
            await PowerCmd.Remove(power);
        if (target.IsPlayer)
            target.Player!.PlayerCombatState?.OrbQueue.Clear();
        if (combatState is not null &&
            target.Side == CombatSide.Enemy &&
            wasPrimaryEnemy &&
            livingTeammates.Length > 0 &&
            livingTeammates.All(teammate => teammate.IsSecondaryEnemy))
        {
            foreach (Creature teammate in livingTeammates.Where(teammate => teammate.IsAlive))
            {
                DamageResult teammateDeath = teammate.LoseHpInternal(
                    teammate.CurrentHp,
                    ValueProp.Unblockable | ValueProp.Unpowered);
                if (teammateDeath.UnblockedDamage > 0)
                    await Hook.AfterCurrentHpChanged(runState, teammate.CombatState,
                        teammate, -teammateDeath.UnblockedDamage);
                await ResolvePotentialDeath(runState, combatState, teammate, teammateDeath);
            }
        }
        else if (combatState is not null && target.Player is { IsOstyAlive: true } player)
        {
            // 原版：玩家死亡时连带击杀存活的 Osty。
            await Kill(player.Osty!);
        }

        return result;
    }

    public static async Task<decimal> GainBlock(
        ICombatState combatState,
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (combatState.IsOverOrEnding())
        {
            return 0m;
        }
        if (creature.IsDead)
        {
            return 0m;
        }

        await Hook.BeforeBlockGained(combatState, creature, amount, props, cardSource);
        decimal modifiedAmount = Math.Max(
            Hook.ModifyBlock(
                combatState,
                creature,
                amount,
                props,
                cardSource,
                cardPlay,
                out IEnumerable<AbstractModel> modifiers),
            0m);
        await Hook.AfterModifyingBlockAmount(
            combatState,
            modifiedAmount,
            cardSource,
            cardPlay,
            modifiers);
        if (modifiedAmount > 0m)
        {
            creature.GainBlockInternal(modifiedAmount);
            if (combatState is CombatState concreteState && concreteState.IsLiveCombat())
                concreteState.SemanticHistory.RecordBlockGain(concreteState, props, cardPlay);
        }

        await Hook.AfterBlockGained(combatState, creature, modifiedAmount, props, cardSource);
        return modifiedAmount;
    }

    public static async Task LoseBlock(
        ICombatState combatState,
        Creature target,
        decimal amount,
        Creature? remover)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(target);
        if (amount <= 0m || target.IsDead)
        {
            return;
        }

        int blockBefore = target.Block;
        target.LoseBlockInternal(amount);
        if (blockBefore > 0 && target.Block == 0)
        {
            await Hook.AfterBlockBroken(combatState, target, remover);
        }
    }

    /// <summary>治疗。偏离 #45：普通治疗不经过 hook 折叠；休息点专用的
    /// <c>Hook.ModifyRestSiteHealAmount</c> 只在 <c>RestSiteRoom</c> 的治疗公式中分发，
    /// 其他治疗效果直接应用。</summary>
    public static async Task Heal(Creature creature, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(creature);
        int hpBefore = creature.CurrentHp;
        creature.HealInternal(amount);
        (creature.CombatState as CombatState)?.PredictionSink?.HealApplied(
            creature, amount, hpBefore, creature.CurrentHp);
        if (amount > 0m && creature.CombatState is { } combatState)
            await Hook.AfterCurrentHpChanged(combatState.RunState, combatState, creature, amount);
    }

    /// <summary>Sets current HP directly for effects such as Fur Coat that do not deal damage.</summary>
    public static async Task SetCurrentHp(Creature creature, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(creature);
        decimal previous = creature.CurrentHp;
        creature.SetCurrentHpInternal(amount);
        if (amount != previous)
        {
            IRunState runState = creature.Player?.RunState ?? creature.CombatState?.RunState
                ?? throw new InvalidOperationException("Creature has no run state for HP-change hooks.");
            await Hook.AfterCurrentHpChanged(runState, creature.CombatState,
                creature, amount - previous);
        }
    }

    /// <summary>Sets maximum HP before current HP, preserving the source revival command order.</summary>
    public static async Task SetMaxAndCurrentHp(Creature creature, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(creature);
        creature.SetMaxHpInternal(amount);
        await SetCurrentHp(creature, amount);
    }

    /// <summary>原版 <c>CreatureCmd.SetMaxHp</c>：只改最大生命，返回变化量。原版降到 0 时会击杀；
    /// 这里 <see cref="Creature.SetMaxHpInternal"/> 把下限夹在 1，那条分支不可达。</summary>
    public static Task<decimal> SetMaxHp(Creature creature, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(creature);
        int oldMaxHp = creature.MaxHp;
        creature.SetMaxHpInternal(amount);
        return Task.FromResult<decimal>(creature.MaxHp - oldMaxHp);
    }

    /// <summary>Increases maximum HP and heals the actual gained amount.
    /// 偏离 #162：省略真实命令对 CurrentMapPointHistoryEntry.MaxHpGained 的记录；
    /// 本项目没有对应的地图 history 系统。</summary>
    public static async Task GainMaxHp(Creature creature, decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentException("Maximum HP gain cannot be negative.", nameof(amount));
        }

        int maxHpBefore = creature.MaxHp;
        creature.SetMaxHpInternal(creature.MaxHp + amount);
        await Heal(creature, creature.MaxHp - maxHpBefore);
    }

    /// <summary>Reduces maximum HP through the source damage/death lifecycle when current HP exceeds the new maximum.</summary>
    public static async Task LoseMaxHp(
        IRunState runState,
        Creature creature,
        decimal amount,
        bool isFromCard)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(creature);
        if (amount < 0m)
        {
            throw new ArgumentException("Maximum HP loss cannot be negative.", nameof(amount));
        }

        ICombatPredictionSink? predictionSink = (creature.CombatState as CombatState)?.PredictionSink;
        decimal newMaxHp = creature.MaxHp - amount;
        if (newMaxHp < creature.CurrentHp)
        {
            ValueProp props = ValueProp.Unblockable | ValueProp.Unpowered;
            if (isFromCard)
            {
                props |= ValueProp.Move;
            }

            await Damage(runState, creature, creature.CurrentHp - newMaxHp, props);
        }

        int maxHpBefore = creature.MaxHp;
        creature.SetMaxHpInternal(Math.Max(1m, newMaxHp));
        predictionSink?.MaxHpLost(
            creature, amount, maxHpBefore, creature.MaxHp, isFromCard);
    }
}
