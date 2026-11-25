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
        CardPlay? cardPlay,
        ModifyDamageHookType modifyDamageHookType = ModifyDamageHookType.All)
    {
        if (dealer?.IsDead == true)
            return targets.Select(target => new DamageResult(target, props)).ToArray();

        List<Creature> targetList = targets.ToList();
        var pending = new List<(DamageResult Result, bool Prevented)>();

        // One hit applies damage to every target before any result hook or death changes listeners.
        foreach (Creature target in targetList)
        {
            if (target.IsDead)
                continue;

            pending.AddRange(await DamageSingle(combatState, target, amount, props, dealer, cardSource, cardPlay, modifyDamageHookType));
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
            await ResolvePotentialDeath(combatState.RunState, combatState, result.Receiver, result, cardSource);

        var results = new List<DamageResult>(pending.Count);
        foreach (var (result, _) in pending)
        {
            (combatState as CombatState)?.Observer?.DamageResolved(dealer, result);
            results.Add(result);
        }
        return results;
    }

    /// <summary>原版 v0.114.0 <c>CreatureCmd.Damage</c> 对单个目标的结算：ModifyDamage → 空跑
    /// <see cref="GetDamageResults"/> → <c>BeforeDamageReceived</c> → <see cref="ApplyDamageResults"/> → 按
    /// <see cref="DamageResults.AllResults"/> 顺序写伤害历史。结果顺序：原目标 → 宠物主人格挡 → 格挡前转移 → 格挡后转移。</summary>
    private static async Task<IReadOnlyList<(DamageResult Result, bool Prevented)>> DamageSingle(
        ICombatState combatState,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        ModifyDamageHookType modifyDamageHookType)
    {
        if (!Hook.ShouldAllowHitting(combatState, target))
        {
            var prevented = new DamageResult(target, props);
            return [(prevented, true)];
        }

        IRunState runState = combatState.RunState;
        decimal modifiedAmount = Hook.ModifyDamage(combatState, target, dealer, amount, props, cardSource, cardPlay, out _, modifyDamageHookType);
        DamageResults damageResults = GetDamageResults(runState, combatState, target, modifiedAmount, props,
            dealer, cardSource, cardPlay);
        await Hook.BeforeDamageReceived(combatState, target, modifiedAmount, props, dealer, cardSource);
        await ApplyDamageResults(runState, combatState, damageResults);

        // 原版 v0.114.0 在结算之后按 AllResults 顺序写伤害历史。
        var results = new List<(DamageResult Result, bool Prevented)>(4);
        foreach (DamageResult result in damageResults.AllResults())
        {
            (combatState as CombatState)?.DamageHistory.Record(result, result.Receiver, dealer, cardSource, combatState);
            results.Add((result, false));
        }
        return results;
    }

    /// <summary>原版 v0.114.0 <c>CreatureCmd.GetDamageResultsPreview</c>：空跑结算，返回伤害若真打出会产生的全部结果
    /// （含转移给 Wuppy/Osty 的部分），不改任何状态。原版供卡面 <c>DamageVar</c> 等预览使用；模拟器没有卡面预览（偏离 #148），
    /// 这里只保留入口。</summary>
    public static IReadOnlyList<DamageResult> GetDamageResultsPreview(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        decimal damage,
        ValueProp props,
        Creature? dealer,
        CardModel cardSource)
    {
        return GetDamageResults(runState, combatState, target, damage, props, dealer, cardSource, null)
            .AllResults().ToList();
    }

    /// <summary>原版 v0.114.0 <c>CreatureCmd.GetDamageResults</c>：用 dryRun 的 <see cref="Creature.LoseHpInternal"/> /
    /// <see cref="Creature.DamageBlockInternal"/> 算出各承受者的结果，不改状态。格挡由宠物主人承担；未格挡伤害经 BeforeOsty
    /// 阶段后由 <see cref="Hook.ModifyUnblockedDamageTarget"/> 决定承受者，再对承受者跑 AfterOsty；换了承受者时，其溢出伤害
    /// 再对原目标跑一次 AfterOsty。命中宠物且未转移时，主人另得一条只含格挡信息的结果。</summary>
    private static DamageResults GetDamageResults(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        decimal damage,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        Creature blockOwner = target.PetOwner?.Creature ?? target;
        var beforeOstyModifiers = new List<AbstractModel>();
        var beforeWuppyModifiers = new List<AbstractModel>();
        var afterOstyModifiers = new List<AbstractModel>();
        var afterOstyModifiers2 = new List<AbstractModel>();
        DamageResult? preBlockRedirect = null;
        DamageResult? unblockedRedirect = null;
        DamageResult? blockRedirect = null;
        Creature? preBlockTarget = Hook.ModifyPreBlockDamageTarget(combatState, target, damage, props, dealer);
        decimal remaining = damage;
        IEnumerable<AbstractModel> modifiers;
        if (preBlockTarget is not null)
        {
            decimal hpLoss = Hook.ModifyHpLost(runState, combatState, target, remaining, props,
                dealer, cardSource, HpLossHookPhase.BeforeWuppy, out modifiers);
            beforeWuppyModifiers.AddRange(modifiers);
            HpLossResult absorbed = preBlockTarget.LoseHpInternal(hpLoss, dryRun: true);
            preBlockRedirect = new DamageResult(preBlockTarget, props)
            {
                UnblockedDamage = absorbed.HpLost,
                WasTargetKilled = absorbed.Killed,
                OverkillDamage = absorbed.OverkillAmount,
                IntendedHpLoss = (int)remaining,
                OriginalTarget = target,
            };
            remaining = absorbed.OverkillAmount;
        }

        decimal blocked = blockOwner.DamageBlockInternal(remaining, props, dryRun: true);
        remaining = Math.Max(remaining - blocked, 0m);
        decimal unblocked = Hook.ModifyHpLost(runState, combatState, target, remaining, props,
            dealer, cardSource, HpLossHookPhase.BeforeOsty, out modifiers);
        beforeOstyModifiers.AddRange(modifiers);
        Creature receiver = Hook.ModifyUnblockedDamageTarget(combatState, target, unblocked, props, dealer);
        unblocked = Hook.ModifyHpLost(runState, combatState, receiver, unblocked, props,
            dealer, cardSource, HpLossHookPhase.AfterOsty, out modifiers);
        afterOstyModifiers.AddRange(modifiers);
        HpLossResult receivedLoss = receiver.LoseHpInternal(unblocked, dryRun: true);
        var received = new DamageResult(receiver, props)
        {
            UnblockedDamage = receivedLoss.HpLost,
            WasTargetKilled = receivedLoss.Killed,
            OverkillDamage = receivedLoss.OverkillAmount,
            IntendedHpLoss = (int)remaining,
            OriginalTarget = target,
        };
        remaining = receivedLoss.OverkillAmount;
        // 原版在空跑阶段（格挡尚未扣减）判定，两项都看原始目标的格挡。
        bool blockBroken = (decimal)target.Block <= blocked && blocked > 0m;
        bool fullyBlocked = !props.HasFlag(ValueProp.Unblockable) &&
                            (blocked > 0m || target.Block > 0) && (int)unblocked == 0;

        DamageResult original;
        if (ReferenceEquals(target, receiver))
        {
            original = received with
            {
                BlockedDamage = (int)blocked, WasBlockBroken = blockBroken, WasFullyBlocked = fullyBlocked,
            };
            if (!ReferenceEquals(original.Receiver, blockOwner))
            {
                blockRedirect = new DamageResult(blockOwner, props)
                {
                    UnblockedDamage = 0,
                    WasTargetKilled = false,
                    OverkillDamage = 0,
                    IntendedHpLoss = (int)remaining,
                    BlockedDamage = (int)blocked,
                    WasBlockBroken = blockBroken,
                    WasFullyBlocked = fullyBlocked,
                };
            }
        }
        else
        {
            unblockedRedirect = received;
            decimal overflow = Hook.ModifyHpLost(runState, combatState, target, remaining, props,
                dealer, cardSource, HpLossHookPhase.AfterOsty, out modifiers);
            afterOstyModifiers2.AddRange(modifiers);
            DamageResult overflowResult;
            if (overflow > 0m)
            {
                HpLossResult overflowLoss = target.LoseHpInternal(overflow, dryRun: true);
                overflowResult = new DamageResult(target, props)
                {
                    UnblockedDamage = overflowLoss.HpLost,
                    WasTargetKilled = overflowLoss.Killed,
                    OverkillDamage = overflowLoss.OverkillAmount,
                };
            }
            else
            {
                overflowResult = new DamageResult(target, props);
            }

            original = overflowResult with
            {
                IntendedHpLoss = (int)remaining,
                BlockedDamage = (int)blocked,
                WasBlockBroken = blockBroken,
                WasFullyBlocked = fullyBlocked,
            };
        }

        return new DamageResults
        {
            Original = original,
            PreBlockRedirect = preBlockRedirect,
            UnblockedRedirect = unblockedRedirect,
            BlockRedirect = blockRedirect,
            BeforeWuppyHpLossModifiers = beforeWuppyModifiers,
            BeforeOstyHpLossModifiers = beforeOstyModifiers,
            AfterOstyHpLossModifiers = afterOstyModifiers,
            AfterOstyHpLossModifiers2 = afterOstyModifiers2,
        };
    }

    /// <summary>原版 v0.114.0 <c>CreatureCmd.ApplyDamageResults</c>：按固定顺序把空跑结果落到状态上，并在对应位置
    /// 通知 HP 修正者。</summary>
    private static async Task ApplyDamageResults(IRunState runState, ICombatState? combatState, DamageResults results)
    {
        results.PreBlockRedirect?.Receiver.LoseHpInternal(results.PreBlockRedirect.UnblockedDamage);
        results.BlockRedirect?.Receiver.DamageBlockInternal(results.BlockRedirect.BlockedDamage, results.BlockRedirect.Props);
        results.Original.Receiver.DamageBlockInternal(results.Original.BlockedDamage, results.Original.Props);
        if (results.BeforeOstyHpLossModifiers.Count > 0)
            await Hook.AfterModifyingHpLostBeforeOsty(runState, combatState, results.BeforeOstyHpLossModifiers);
        results.UnblockedRedirect?.Receiver.LoseHpInternal(results.UnblockedRedirect.UnblockedDamage);
        if (results.AfterOstyHpLossModifiers.Count > 0)
            await Hook.AfterModifyingHpLostAfterOsty(runState, combatState, results.AfterOstyHpLossModifiers);
        results.Original.Receiver.LoseHpInternal(results.Original.UnblockedDamage);
        if (results.AfterOstyHpLossModifiers2.Count > 0)
            await Hook.AfterModifyingHpLostAfterOsty(runState, combatState, results.AfterOstyHpLossModifiers2);
    }

    private static DamageResult LoseHpForDeathPipeline(Creature creature, decimal amount, ValueProp props)
    {
        HpLossResult loss = creature.LoseHpInternal(amount);
        return new DamageResult(creature, props)
        {
            UnblockedDamage = loss.HpLost,
            WasTargetKilled = loss.Killed,
            OverkillDamage = loss.OverkillAmount,
        };
    }

    /// <summary>Kills a combat creature directly without running the ordinary damage hook pipeline.</summary>
    public static async Task Kill(Creature creature, CardModel? cardSource = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        if (creature.IsDead)
        {
            return;
        }

        ICombatState combatState = creature.CombatState
            ?? throw new InvalidOperationException("Creature has no combat state.");
        DamageResult result = LoseHpForDeathPipeline(
            creature,
            creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered);
        if (result.UnblockedDamage > 0)
            await Hook.AfterCurrentHpChanged(combatState.RunState, combatState,
                creature, -result.UnblockedDamage);
        await ResolvePotentialDeath(combatState.RunState, combatState, creature, result, cardSource);
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

        DamageResult result = LoseHpForDeathPipeline(
            creature,
            creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered);
        if (result.UnblockedDamage > 0)
            await Hook.AfterCurrentHpChanged(runState, creature.CombatState,
                creature, -result.UnblockedDamage);
        await Hook.BeforeDeath(runState, creature.CombatState, creature);
        await ResolveConfirmedDeath(runState, creature.CombatState, creature, result);
    }

    /// <summary>Removes a living creature from combat without running the death lifecycle.</summary>
    public static async Task Escape(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ICombatState? combatState = creature.CombatState;
        if (creature.IsDead || combatState is null || !combatState.IsLiveCombat() ||
            !combatState.ContainsCreature(creature))
        {
            return;
        }

        foreach (Models.Powers.ImpaledPower impaled in creature.Powers.OfType<Models.Powers.ImpaledPower>().ToList())
        {
            await impaled.DislodgeAllArrows(null);
        }

        foreach (PowerModel power in creature.Powers.ToArray())
        {
            power.RemoveInternal();
        }

        combatState.CreatureEscaped(creature);
    }

    /// <summary>Applies run-level damage outside combat through the normal damage hook phases.</summary>
    public static async Task<DamageResult> Damage(
        IRunState runState,
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource = null)
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
            cardSource?.Owner.Creature,
            amount,
            props,
            cardSource,
            null,
            out _);
        Creature? dealer = cardSource?.Owner.Creature;
        // v0.114.0 同一条管线：先空跑，再 BeforeDamageReceived，再结算。战斗外没有转移；出现即说明前提失效。
        DamageResults damageResults = GetDamageResults(runState, null, target, modifiedAmount, props,
            dealer, cardSource, null);
        if (damageResults.BlockRedirect is not null || damageResults.PreBlockRedirect is not null ||
            damageResults.UnblockedRedirect is not null)
        {
            throw new InvalidOperationException("Run-level damage was redirected outside combat.");
        }

        await Hook.BeforeDamageReceived(runState, null, target, modifiedAmount, props, dealer, cardSource);
        await ApplyDamageResults(runState, null, damageResults);
        DamageResult result = damageResults.Original;

        if (result.UnblockedDamage > 0)
            await Hook.AfterCurrentHpChanged(runState, null, target, -result.UnblockedDamage);
        await Hook.AfterDamageReceived(runState, null, target, result, props, dealer, cardSource);
        return await ResolvePotentialDeath(runState, null, target, result, cardSource);
    }

    public static async Task<DamageResult> LoseHp(
        IRunState runState,
        Creature target,
        decimal amount,
        ValueProp props)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(target);
        DamageResult result = LoseHpForDeathPipeline(target, amount, props);
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
        DamageResult result,
        CardModel? cardSource = null)
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
            await Hook.AfterDeath(runState, combatState, target, wasRemovalPrevented: true, source: cardSource);
            int hpBefore = target.CurrentHp;
            await Hook.AfterPreventingDeath(preventer!, target);
            (combatState as CombatState)?.PredictionSink?.DeathPrevented(
                target, preventer!, hpBefore, target.CurrentHp);
            if (!target.IsDead)
                return result;
        }
        return await ResolveConfirmedDeath(runState, combatState, target, result, cardSource);
    }

    private static async Task<DamageResult> ResolveConfirmedDeath(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        DamageResult result,
        CardModel? cardSource = null)
    {
        target.InvokeDiedEvent();
        bool shouldRemove = combatState is not null &&
            Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, target);
        await Hook.AfterDeath(runState, combatState, target, source: cardSource);
        bool wasPrimaryEnemy = target.IsPrimaryEnemy;
        Creature[] livingTeammates = combatState?.GetCreaturesOnSide(target.Side)
            .Where(teammate => !ReferenceEquals(teammate, target) && teammate.IsAlive)
            .ToArray() ?? Array.Empty<Creature>();
        PowerModel[] powersToRemove = PowersRemovedAfterDeath(combatState, target);
        // v0.114.0：正在行动或正在攻击的生物推迟移出战斗与清空能力，分别由 MonsterModel.PerformMove 与
        // AttackCommand.Execute 收尾处理。
        bool deferCleanup = target.Monster?.IsPerformingMove == true || target.IsAttacking;
        if (shouldRemove && target.Side == CombatSide.Enemy &&
            combatState!.Enemies.Contains(target) && !deferCleanup)
            combatState.RemoveCreature(target);
        if (!deferCleanup)
            await RemovePowers(powersToRemove);
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
                DamageResult teammateDeath = LoseHpForDeathPipeline(
                    teammate,
                    teammate.CurrentHp,
                    ValueProp.Unblockable | ValueProp.Unpowered);
                if (teammateDeath.UnblockedDamage > 0)
                    await Hook.AfterCurrentHpChanged(runState, teammate.CombatState,
                        teammate, -teammateDeath.UnblockedDamage);
                await ResolvePotentialDeath(runState, combatState, teammate, teammateDeath, cardSource);
            }
        }
        else if (combatState is not null && target.Player is { IsOstyAlive: true } player)
        {
            // 原版：玩家死亡时连带击杀存活的 Osty。
            await Kill(player.Osty!);
        }

        // 原版 CreatureCmd.Kill：死亡结算完、全体玩家都已死亡时 CombatManager.LoseCombat()。
        if (target.IsPlayer && combatState is CombatState concrete && !concrete.HasLivingPlayerCreature())
            concrete.Engine?.LoseCombat();
        return result;
    }

    /// <summary>原版 <c>Creature.RemoveAllPowersAfterDeath</c> 的筛选：死亡时应移除的能力。</summary>
    internal static PowerModel[] PowersRemovedAfterDeath(ICombatState? combatState, Creature target) =>
        target.Powers.Where(power => combatState is null ||
            (Hook.ShouldPowerBeRemovedOnDeath(combatState, power) &&
             Hook.ShouldPowerBeRemovedAfterOwnerDeath(combatState, power))).ToArray();

    /// <summary>移除死亡生物的能力并逐个触发 <c>AfterRemoved</c>（v0.114.0 攻击收尾与怪物行动收尾同 Kill 共用）。</summary>
    internal static async Task RemovePowersAfterDeath(ICombatState? combatState, Creature target) =>
        await RemovePowers(PowersRemovedAfterDeath(combatState, target));

    private static async Task RemovePowers(IEnumerable<PowerModel> powers)
    {
        foreach (PowerModel power in powers)
            await PowerCmd.Remove(power);
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
        if (combatState.IsOverOrEnding() || amount <= 0m || target.IsDead)
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
        if (!creature.IsPlayer && creature.CombatState?.IsEnding() == true)
            return;

        int hpBefore = creature.CurrentHp;
        creature.HealInternal(amount);
        (creature.CombatState as CombatState)?.PredictionSink?.HealApplied(
            creature, amount, hpBefore, creature.CurrentHp);
        if (amount > 0m && creature.CombatState is { } combatState)
            await Hook.AfterCurrentHpChanged(combatState.RunState, combatState, creature, amount);
    }

    /// <summary>Sets current HP directly for effects such as Fur Coat that do not deal damage,
    /// running the death pipeline when HP drops to zero.</summary>
    public static async Task SetCurrentHp(Creature creature, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(creature);
        decimal previous = creature.CurrentHp;
        creature.SetCurrentHpInternal(amount);
        IRunState? runState = creature.Player?.RunState ?? creature.CombatState?.RunState;
        if (amount != previous)
        {
            await Hook.AfterCurrentHpChanged(
                runState ?? throw new InvalidOperationException("Creature has no run state for HP-change hooks."),
                creature.CombatState,
                creature,
                amount - previous);
        }

        if (creature.IsDead &&
            (creature.CombatState is { } combatState ? combatState.IsLiveCombat() : creature.IsPlayer))
        {
            await ResolvePotentialDeath(
                runState ?? throw new InvalidOperationException("Creature has no run state for death resolution."),
                creature.CombatState,
                creature,
                new DamageResult(creature, ValueProp.Unblockable | ValueProp.Unpowered)
                {
                    UnblockedDamage = 0,
                    WasTargetKilled = true,
                });
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
        CardModel? cardSource)
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
            if (cardSource is not null)
            {
                props |= ValueProp.Move;
            }

            if (creature.CombatState is { } combatState)
                await Damage(combatState, [creature], creature.CurrentHp - newMaxHp, props,
                    cardSource?.Owner.Creature, cardSource, null);
            else
                await Damage(runState, creature, creature.CurrentHp - newMaxHp, props, cardSource);
        }

        int maxHpBefore = creature.MaxHp;
        creature.SetMaxHpInternal(Math.Max(1m, newMaxHp));
        predictionSink?.MaxHpLost(
            creature, amount, maxHpBefore, creature.MaxHp, cardSource is not null);
    }
}
