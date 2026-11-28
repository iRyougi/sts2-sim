using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Afflictions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Hooks;

/// <summary>
/// 全局 Hook 静态调度器(无状态——监听器全部来自传入的 IRunState/ICombatState,并行安全)。
/// Upstream v0.111.0 has 148 public dispatchers; MCP reports 149 by including a private helper.
/// Deviation #16 and docs/superpowers/reviews/2026-09-12-plan-08b-4-hook-audit.md track
/// missing, adapted and unresolved call-site semantics; matching names do not establish parity.
/// </summary>
public static class Hook
{
    public static bool ShouldFlush(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldFlush))
        {
            if (!model.ShouldFlush(player)) return false;
        }
        return true;
    }

    public static bool ShouldProcurePotion(IRunState runState, ICombatState? combatState, PotionModel potion, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!model.ShouldProcurePotion(potion, player)) return false;
        }
        return true;
    }

    public static async Task AfterItemPurchased(IRunState runState, Player player,
        MerchantEntry itemPurchased, int goldSpent)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterItemPurchased(player, itemPurchased, goldSpent);
            model.InvokeExecutionFinished();
        }
    }

    public static CardModel ModifyCardBeingAddedToDeck(IRunState runState, CardModel card)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (model.TryModifyCardBeingAddedToDeck(card, out CardModel? replacement) && replacement is not null)
            {
                card = replacement;
            }
            model.InvokeExecutionFinished();
        }
        return card;
    }

    public static void ModifyMerchantCardCreationResults(IRunState runState, Player player, List<CardModel> cards)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            model.ModifyMerchantCardCreationResults(player, cards);
        }
    }

    public static Map.ActMap ModifyGeneratedMap(IRunState runState, Map.ActMap map, int actIndex)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            map = model.ModifyGeneratedMap(runState, map, actIndex);
        }
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            map = model.ModifyGeneratedMapLate(runState, map, actIndex);
        }
        return map;
    }

    public static async Task AfterMapGenerated(IRunState runState, Map.ActMap map, int actIndex)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterMapGenerated(map, actIndex);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeCardRemoved(IRunState runState, CardModel card)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null).ToArray())
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.BeforeCardRemoved(card);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterActEntered(IRunState runState)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterActEntered();
            model.InvokeExecutionFinished();
        }
    }

    /// <summary>Dispatches before room setup, while persistent deck cards can still transform.</summary>
    public static async Task BeforeRoomEntered(IRunState runState, Rooms.AbstractRoom room)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null).ToArray())
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.BeforeRoomEntered(room);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterRoomEntered(IRunState runState, Rooms.AbstractRoom room)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterRoomEntered(room);
            model.InvokeExecutionFinished();
        }
    }

    public static decimal ModifyRestSiteHealAmount(
        IRunState runState,
        Creature creature,
        decimal amount)
    {
        decimal result = amount;
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            result = model.ModifyRestSiteHealAmount(creature, result);
        }

        return result;
    }

    public static async Task AfterRestSiteHeal(IRunState runState, Player player, bool isMimicked = false)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterRestSiteHeal(player, isMimicked);
            model.InvokeExecutionFinished();
        }
    }

    public static IEnumerable<AbstractModel> ModifyRestSiteHealRewards(
        IRunState runState, Player player, List<Reward> rewards, bool isMimicked)
    {
        var modifiers = new List<AbstractModel>();
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (model.TryModifyRestSiteHealRewards(player, rewards, isMimicked))
            {
                modifiers.Add(model);
            }
        }
        return modifiers;
    }

    public static void ModifyAvailableRestSiteDecisions(
        IRunState runState,
        Player player,
        List<RestSiteDecision> decisions)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            model.ModifyAvailableRestSiteDecisions(runState, player, decisions);
        }
    }

    public static decimal ModifyGoldGained(IRunState runState, Player player, decimal amount)
    {
        decimal result = amount;
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(item)) continue;
            result = item.ModifyGoldGained(player, result);
        }
        return result;
    }

    public static async Task AfterGoldGained(IRunState runState, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterGoldGained(player);
            model.InvokeExecutionFinished();
        }
    }

    public static decimal ModifyMerchantPrice(
        IRunState runState,
        Player player,
        MerchantEntry entry,
        decimal originalPrice)
    {
        decimal result = originalPrice;
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            result = model.ModifyMerchantPrice(player, entry, result);
        }

        return result;
    }

    public static bool ShouldRefillMerchantEntry(
        IRunState runState,
        MerchantEntry entry,
        Player player) =>
        runState.IterateHookListeners(null)
            .Any(model => IsEligibleHookListener(model) && model.ShouldRefillMerchantEntry(entry, player));

    public static void ModifyRewards(IRunState runState, Player player, List<Reward> rewards, RoomType roomType)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(player.Creature.CombatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            model.ModifyRewards(player, rewards, roomType);
        }
    }

    public static bool TryReplaceDefaultRoomRewards(
        IRunState runState,
        Player player,
        List<Reward> rewards,
        AbstractRoom room)
    {
        AbstractModel? replacer = null;
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            var candidate = new List<Reward>();
            if (model.TryReplaceDefaultRoomRewards(player, candidate, room))
            {
                if (replacer is null)
                {
                    replacer = model;
                    rewards.AddRange(candidate);
                }
            }
        }
        return replacer is not null;
    }

    public static CardCreationOptions ModifyCardRewardCreationOptions(IRunState runState, Player player, CardCreationOptions options)
    {
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(listener)) continue;
            options = listener.ModifyCardRewardCreationOptions(player, options);
        }
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(listener)) continue;
            options = listener.ModifyCardRewardCreationOptionsLate(player, options);
        }
        return options;
    }

    public static async Task BeforeCombatRewardOffered(Rewards.RewardsSet rewards, IRunState runState, CombatRoom room)
    {
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(listener)) continue;
            await listener.BeforeCombatRewardOffered(rewards, room);
            listener.InvokeExecutionFinished();
        }
    }

    public static IReadOnlyList<AbstractModel> TryModifyCardRewardOptions(
        IRunState runState, Player player, List<CardModel> options, CardCreationOptions creationOptions, bool late)
    {
        var modifiers = new List<AbstractModel>();
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(listener)) continue;
            bool changed = late ? listener.TryModifyCardRewardOptionsLate(player, options, creationOptions)
                : listener.TryModifyCardRewardOptions(player, options, creationOptions);
            if (changed) modifiers.Add(listener);
        }
        return modifiers;
    }

    public static CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option,
        out List<AbstractModel> modifiers) => TryModifyCardRewardOptionLate(
            runState, player, option,
            new CardCreationOptions([player.Character.CardPool], CardCreationSource.Other, CardRarityOddsType.None)
                .WithFlags(CardCreationFlags.IsCardReward), out modifiers);

    public static CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option,
        CardCreationOptions creationOptions,
        out List<AbstractModel> modifiers)
    {
        CardModel current = option;
        bool participated = false;
        modifiers = new List<AbstractModel>();
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(listener)) continue;
            CardModel? candidate = listener.TryModifyCardRewardOptionLate(runState, player, current, creationOptions);
            if (candidate is not null)
            {
                current = candidate;
                participated = true;
                modifiers.Add(listener);
            }
        }

        return participated ? current : null;
    }

    public static void AfterModifyingCardRewardOptions(
        IRunState runState,
        Player player,
        IReadOnlyList<CardModel> options,
        IEnumerable<AbstractModel> modifiers)
    {
        foreach (AbstractModel modifier in modifiers)
        {
            modifier.AfterModifyingCardRewardOptions(runState, player, options);
            modifier.InvokeExecutionFinished();
        }
    }

    public static Task BeforePotionUsed(IRunState runState, PotionModel potion, Player player) =>
        BeforePotionUsed(runState, player.Creature.CombatState, potion, player.Creature);

    public static async Task BeforePotionUsed(
        IRunState runState,
        ICombatState? combatState,
        PotionModel potion,
        Creature? target)
    {
        foreach (AbstractModel model in RunRaw(runState, combatState, eagerBothPhases: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.BeforePotionUsed(potion, target);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterPotionProcured(IRunState runState, ICombatState? combatState, PotionModel potion)
    {
        foreach (AbstractModel model in RunRaw(runState, combatState, eagerBothPhases: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterPotionProcured(potion);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterPotionDiscarded(IRunState runState, ICombatState? combatState, PotionModel potion)
    {
        foreach (AbstractModel model in RunRaw(runState, combatState, eagerBothPhases: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterPotionDiscarded(potion);
            model.InvokeExecutionFinished();
        }
    }

    public static void ModifyCardRewardAlternatives(IRunState runState, Player player,
        CardReward reward, List<CardRewardAlternative> alternatives)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            model.TryModifyCardRewardAlternatives(player, reward, alternatives);
        }
    }

    public static bool CanRerollCardReward(IRunState runState, Player player, CardReward reward)
    {
        bool canReroll = false;
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            canReroll |= model.TryEnableCardRewardReroll(player, reward);
        }
        return canReroll;
    }
    public static async Task AfterPotionUsed(IRunState runState, PotionModel potion, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterPotionUsed(potion, player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterStarsGained(ICombatState combatState, int amount, Player gainer)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterStarsGained, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterStarsGained(amount, gainer);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterStarsSpent(ICombatState combatState, int amount, Player spender)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterStarsSpent, notifyExecFinished: true))
        {
            await model.AfterStarsSpent(amount, spender);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterEnergySpent(ICombatState combatState, CardModel card, int amount)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterEnergySpent, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterEnergySpent(card, amount);
            model.InvokeExecutionFinished();
        }
    }

    public static IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IRunState runState, IReadOnlySet<RoomType> roomTypes)
    {
        IReadOnlySet<RoomType> readOnlySet = new HashSet<RoomType>(roomTypes);
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(item)) continue;
            readOnlySet = item.ModifyUnknownMapPointRoomTypes(readOnlySet);
        }
        return readOnlySet;
    }

    public static EventModel ModifyNextEvent(IRunState runState, EventModel currentEvent)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            currentEvent = model.ModifyNextEvent(currentEvent);
        }
        return currentEvent;
    }

    public static float ModifyOddsIncreaseForUnrolledRoomType(IRunState runState, RoomType roomType, float oddsIncrease)
    {
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(item)) continue;
            oddsIncrease = item.ModifyOddsIncreaseForUnrolledRoomType(roomType, oddsIncrease);
        }
        return oddsIncrease;
    }

    // 偏离 #17:原签名 (IRunState, Player, RoomType),Plan 03 恢复。
    // 注意保持游戏的 OR-fold 写法:flag 为 true 后,|| 短路使后续监听器不再被调用,但循环走完。
    public static bool ShouldForcePotionReward(IRunState runState, RoomType roomType)
    {
        bool flag = false;
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(item)) continue;
            flag = flag || item.ShouldForcePotionReward(roomType);
        }
        return flag;
    }

    public static bool ShouldProceedToNextMapPoint(IRunState runState)
    {
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(item)) continue;
            if (!item.ShouldProceedToNextMapPoint())
            {
                return false;
            }
        }
        return true;
    }

    public static bool ShouldAllowFreeTravel(IRunState runState)
    {
        bool flag = false;
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(item)) continue;
            flag = flag || item.ShouldAllowFreeTravel();
        }
        return flag;
    }

    // 偏离 #173：计划正文误写为 OR 折叠；权威源码采用 AND/veto 语义，任一监听器返回 false 即抑制该玩家的宝箱奖励。
    public static bool ShouldGenerateTreasure(IRunState runState, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!model.ShouldGenerateTreasure(player))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ShouldAllowAncient(IRunState runState, Player player, AncientEventModel ancientEvent)
    {
        bool result = true;
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(listener)) continue;
            result &= listener.ShouldAllowAncient(runState, player, ancientEvent);
        }
        return result;
    }

    /// <summary>
    /// 偏离 #28: 游戏原版签名为 (IRunState, ICombatState?),本计划战斗期 hook 只从 ICombatState 派生 IRunState。
    /// 游戏 <c>Hook.IterateCombatHookListeners</c>（#1072）：首次 MoveNext 时检查结束闸门，然后只遍历
    /// <c>combatState.IterateHookListeners()</c>（逐个生物：能力 → 怪物，或玩家的遗物、药水、充能球、牌堆里的牌），
    /// 不经过 RunState，所以没有牌组牌。
    /// </summary>
    private static IEnumerable<AbstractModel> IterateCombatHookListeners(ICombatState combatState)
    {
        if (combatState.IsOverOrEnding() && !combatState.IsStarting())
            yield break;
        // Actual dispatch loops apply lifecycle eligibility; this helper preserves raw capture.
        foreach (AbstractModel model in combatState.IterateHookListeners())
            yield return model;
    }

    // Source iterators and materialized arrays remain RAW candidates. Qualification is fresh
    // at each actual dispatch, matching native yield-time Contains without changing capture.
    // Scope B deliberately retains legacy passthrough for five known kinds (see deviations).
    // Plan 09-O: the same type switch, read from the kind each model carries since construction (no type tests per visit).
    private static bool IsEligibleHookListener(AbstractModel model)
    {
        switch (model?.HookListenerKind ?? HookOverrideIndex.ListenerKind.Unknown)
        {
            case HookOverrideIndex.ListenerKind.Power:
                PowerModel power = System.Runtime.CompilerServices.Unsafe.As<PowerModel>(model)!;
                return power.Owner.CombatState is not null && (power.Owner.Player?.IsActiveForHooks ?? true);
            case HookOverrideIndex.ListenerKind.Monster:
                return System.Runtime.CompilerServices.Unsafe.As<MonsterModel>(model)!.Creature.CombatState is not null;
            case HookOverrideIndex.ListenerKind.Orb:
                OrbModel orb = System.Runtime.CompilerServices.Unsafe.As<OrbModel>(model)!;
                return !orb.HasBeenRemovedFromState && orb.Owner.IsActiveForHooks;
            case HookOverrideIndex.ListenerKind.Passive:
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(model), model?.GetType(),
                    "Unsupported hook listener model type.");
        }
    }

    /// <summary>
    /// Plan 09-O: a known listener kind whose type inherits <see cref="AbstractModel"/>'s no-op for this hook is passed over
    /// without the call. The only observable part of such a visit, the ExecutionFinished notification of an eligible listener
    /// (diagnostic subscribers only), is still raised; eligibility is a pure read for known kinds.
    /// </summary>
    private static bool SkipNoOp(AbstractModel model, HookOverrideIndex.HookSlot slot)
    {
        if (!HookOverrideIndex.Skips(model, slot)) return false;
        if (model.HasExecutionFinishedSubscribers && IsEligibleHookListener(model)) model.InvokeExecutionFinished();
        return true;
    }

    private static bool HasNoKnownModifiersAndCleanCombat(
        ICombatState combatState,
        IEnumerable<AbstractModel> modifiers) =>
        modifiers is ICollection<AbstractModel> { Count: 0 } &&
        combatState is CombatState concrete &&
        (concrete.HookListenerListWithMask().ActiveHi & HookOverrideIndex.UnknownOrNullSentinelHiBit) == 0;

    private static bool HasNoKnownModifiersAndCleanRunCombat(
        IRunState runState,
        ICombatState? combatState,
        IEnumerable<AbstractModel> modifiers)
    {
        if (modifiers is not ICollection<AbstractModel> { Count: 0 } ||
            combatState is not CombatState concrete ||
            !CombatState.IsConcreteRunOrSnapshot(runState))
        {
            return false;
        }
        var deck = concrete.DeckHookListenerListWithMask(runState.Players);
        if ((deck.ActiveHi & HookOverrideIndex.UnknownOrNullSentinelHiBit) != 0) return false;
        var combat = concrete.HookListenerListWithMask();
        return (combat.ActiveHi & HookOverrideIndex.UnknownOrNullSentinelHiBit) == 0;
    }

    private static CombatSlotListeners CombatSlot(
        ICombatState combatState,
        HookOverrideIndex.HookSlot slot,
        bool gated = true,
        bool notifyExecFinished = false,
        bool snapshotFallback = false)
    {
        if (combatState is CombatState concrete)
        {
            if (gated && concrete.IsOverOrEnding() && !concrete.IsStarting())
                return default;
            CombatState.ListenerListWithMask slice = concrete.HookListenerListWithMask();
            bool active = notifyExecFinished ? slice.HasActiveOrExecFinished(slot) : slice.HasActive(slot);
            return active ? new CombatSlotListeners(slice.List, null, slot, notifyExecFinished) : default;
        }

        if (gated && combatState.IsOverOrEnding() && !combatState.IsStarting())
            return default;
        IEnumerable<AbstractModel> fallback = snapshotFallback
            ? combatState.IterateHookListeners().ToArray()
            : combatState.IterateHookListeners();
        return new CombatSlotListeners(null, fallback, slot, notifyExecFinished);
    }

    private static CombatRawListeners CombatRaw(
        ICombatState combatState,
        bool gated = true,
        bool snapshotFallback = false)
    {
        if (combatState is CombatState concrete)
        {
            if (gated && concrete.IsOverOrEnding() && !concrete.IsStarting())
                return default;
            List<AbstractModel> list = concrete.HookListenerList();
            return list.Count > 0 ? new CombatRawListeners(list, null) : default;
        }

        if (gated && combatState.IsOverOrEnding() && !combatState.IsStarting())
            return default;
        IEnumerable<AbstractModel> fallback = snapshotFallback
            ? combatState.IterateHookListeners().ToArray()
            : combatState.IterateHookListeners();
        return new CombatRawListeners(null, fallback);
    }

    private static RunSlotListeners RunSlot(
        IRunState runState,
        ICombatState? combatState,
        HookOverrideIndex.HookSlot slot,
        bool eagerBothPhases = false,
        bool notifyExecFinished = false)
    {
        if (combatState is CombatState concrete && CombatState.IsConcreteRunOrSnapshot(runState))
        {
            CombatState.ListenerListWithMask deck = concrete.DeckHookListenerListWithMask(runState.Players);
            bool deckActive = notifyExecFinished ? deck.HasActiveOrExecFinished(slot) : deck.HasActive(slot);
            if (eagerBothPhases || !deckActive)
            {
                CombatState.ListenerListWithMask combat = concrete.HookListenerListWithMask();
                bool combatActive = notifyExecFinished ? combat.HasActiveOrExecFinished(slot) : combat.HasActive(slot);
                if (!deckActive && !combatActive)
                    return default;
                return new RunSlotListeners(
                    null,
                    deckActive ? deck.List : null,
                    combatActive ? combat.List : null,
                    null,
                    slot,
                    notifyExecFinished);
            }

            return new RunSlotListeners(concrete, deck.List, null, null, slot, notifyExecFinished);
        }

        IEnumerable<AbstractModel> fallback = eagerBothPhases
            ? runState.IterateHookListeners(combatState).ToArray()
            : runState.IterateHookListeners(combatState);
        return new RunSlotListeners(null, null, null, fallback, slot, notifyExecFinished);
    }

    private static RunRawListeners RunRaw(
        IRunState runState,
        ICombatState? combatState,
        bool eagerBothPhases = false)
    {
        if (combatState is CombatState concrete && CombatState.IsConcreteRunOrSnapshot(runState))
        {
            List<AbstractModel> deck = concrete.DeckHookListenerList(runState.Players);
            if (eagerBothPhases || deck.Count == 0)
            {
                List<AbstractModel> combat = concrete.HookListenerList();
                if (deck.Count == 0 && combat.Count == 0)
                    return default;
                return new RunRawListeners(null, deck.Count > 0 ? deck : null, combat.Count > 0 ? combat : null, null);
            }

            return new RunRawListeners(concrete, deck, null, null);
        }

        IEnumerable<AbstractModel> fallback = eagerBothPhases
            ? runState.IterateHookListeners(combatState).ToArray()
            : runState.IterateHookListeners(combatState);
        return new RunRawListeners(null, null, null, fallback);
    }

    private readonly struct CombatSlotListeners(
        List<AbstractModel>? list,
        IEnumerable<AbstractModel>? fallback,
        HookOverrideIndex.HookSlot slot,
        bool notifyExecFinished)
    {
        public Enumerator GetEnumerator() => new(list, fallback, slot, notifyExecFinished);

        public struct Enumerator(
            List<AbstractModel>? list,
            IEnumerable<AbstractModel>? fallback,
            HookOverrideIndex.HookSlot slot,
            bool notifyExecFinished) : IDisposable
        {
            private readonly List<AbstractModel>? _list = list;
            private IEnumerator<AbstractModel>? _fallbackEnumerator = fallback?.GetEnumerator();
            private readonly HookOverrideIndex.HookSlot _slot = slot;
            private readonly bool _notifyExecFinished = notifyExecFinished;
            private int _index = -1;
            private AbstractModel? _current;

            public AbstractModel Current => _current!;

            public bool MoveNext()
            {
                if (_list is not null)
                {
                    while (++_index < _list.Count)
                    {
                        AbstractModel model = _list[_index];
                        if (_notifyExecFinished ? SkipNoOp(model, _slot) : HookOverrideIndex.Skips(model, _slot))
                            continue;
                        if (!IsEligibleHookListener(model))
                            continue;
                        _current = model;
                        return true;
                    }
                    return false;
                }

                if (_fallbackEnumerator is not null)
                {
                    while (_fallbackEnumerator.MoveNext())
                    {
                        AbstractModel model = _fallbackEnumerator.Current;
                        if (_notifyExecFinished ? SkipNoOp(model, _slot) : HookOverrideIndex.Skips(model, _slot))
                            continue;
                        if (!IsEligibleHookListener(model))
                            continue;
                        _current = model;
                        return true;
                    }
                }
                return false;
            }

            public void Dispose() => _fallbackEnumerator?.Dispose();
        }
    }

    private readonly struct CombatRawListeners(
        List<AbstractModel>? list,
        IEnumerable<AbstractModel>? fallback)
    {
        public Enumerator GetEnumerator() => new(list, fallback);

        public struct Enumerator(
            List<AbstractModel>? list,
            IEnumerable<AbstractModel>? fallback) : IDisposable
        {
            private readonly List<AbstractModel>? _list = list;
            private IEnumerator<AbstractModel>? _fallbackEnumerator = fallback?.GetEnumerator();
            private int _index = -1;
            private AbstractModel? _current;

            public AbstractModel Current => _current!;

            public bool MoveNext()
            {
                if (_list is not null)
                {
                    if (++_index < _list.Count)
                    {
                        _current = _list[_index];
                        return true;
                    }
                    return false;
                }

                if (_fallbackEnumerator is not null && _fallbackEnumerator.MoveNext())
                {
                    _current = _fallbackEnumerator.Current;
                    return true;
                }
                return false;
            }

            public void Dispose() => _fallbackEnumerator?.Dispose();
        }
    }

    private readonly struct RunSlotListeners(
        CombatState? deferredCombat,
        List<AbstractModel>? deck,
        List<AbstractModel>? combat,
        IEnumerable<AbstractModel>? fallback,
        HookOverrideIndex.HookSlot slot,
        bool notifyExecFinished)
    {
        public Enumerator GetEnumerator() => new(deferredCombat, deck, combat, fallback, slot, notifyExecFinished);

        public struct Enumerator(
            CombatState? deferredCombat,
            List<AbstractModel>? deck,
            List<AbstractModel>? combat,
            IEnumerable<AbstractModel>? fallback,
            HookOverrideIndex.HookSlot slot,
            bool notifyExecFinished) : IDisposable
        {
            private CombatState? _deferredCombat = deferredCombat;
            private List<AbstractModel>? _currentList = deck ?? combat;
            private List<AbstractModel>? _nextList = deck is not null ? combat : null;
            private IEnumerator<AbstractModel>? _fallbackEnumerator = fallback?.GetEnumerator();
            private readonly HookOverrideIndex.HookSlot _slot = slot;
            private readonly bool _notifyExecFinished = notifyExecFinished;
            private int _index = -1;
            private AbstractModel? _current;

            public AbstractModel Current => _current!;

            public bool MoveNext()
            {
                while (_currentList is not null)
                {
                    while (++_index < _currentList.Count)
                    {
                        AbstractModel model = _currentList[_index];
                        if (_notifyExecFinished ? SkipNoOp(model, _slot) : HookOverrideIndex.Skips(model, _slot))
                            continue;
                        if (!IsEligibleHookListener(model))
                            continue;
                        _current = model;
                        return true;
                    }

                    if (_nextList is not null)
                    {
                        _currentList = _nextList;
                        _nextList = null;
                        _index = -1;
                        continue;
                    }

                    if (_deferredCombat is not null)
                    {
                        CombatState pending = _deferredCombat;
                        _deferredCombat = null;
                        CombatState.ListenerListWithMask slice = pending.HookListenerListWithMask();
                        bool active = _notifyExecFinished ? slice.HasActiveOrExecFinished(_slot) : slice.HasActive(_slot);
                        _currentList = active ? slice.List : null;
                        _index = -1;
                        continue;
                    }

                    _currentList = null;
                }

                if (_fallbackEnumerator is not null)
                {
                    while (_fallbackEnumerator.MoveNext())
                    {
                        AbstractModel model = _fallbackEnumerator.Current;
                        if (_notifyExecFinished ? SkipNoOp(model, _slot) : HookOverrideIndex.Skips(model, _slot))
                            continue;
                        if (!IsEligibleHookListener(model))
                            continue;
                        _current = model;
                        return true;
                    }
                }
                return false;
            }

            public void Dispose() => _fallbackEnumerator?.Dispose();
        }
    }

    private readonly struct RunRawListeners(
        CombatState? deferredCombat,
        List<AbstractModel>? deck,
        List<AbstractModel>? combat,
        IEnumerable<AbstractModel>? fallback)
    {
        public Enumerator GetEnumerator() => new(deferredCombat, deck, combat, fallback);

        public struct Enumerator(
            CombatState? deferredCombat,
            List<AbstractModel>? deck,
            List<AbstractModel>? combat,
            IEnumerable<AbstractModel>? fallback) : IDisposable
        {
            private CombatState? _deferredCombat = deferredCombat;
            private List<AbstractModel>? _currentList = deck ?? combat;
            private List<AbstractModel>? _nextList = deck is not null ? combat : null;
            private IEnumerator<AbstractModel>? _fallbackEnumerator = fallback?.GetEnumerator();
            private int _index = -1;
            private AbstractModel? _current;

            public AbstractModel Current => _current!;

            public bool MoveNext()
            {
                while (_currentList is not null)
                {
                    if (++_index < _currentList.Count)
                    {
                        _current = _currentList[_index];
                        return true;
                    }

                    if (_nextList is not null)
                    {
                        _currentList = _nextList;
                        _nextList = null;
                        _index = -1;
                        continue;
                    }

                    if (_deferredCombat is not null)
                    {
                        List<AbstractModel> next = _deferredCombat.HookListenerList();
                        _deferredCombat = null;
                        _currentList = next.Count > 0 ? next : null;
                        _index = -1;
                        continue;
                    }

                    _currentList = null;
                }

                if (_fallbackEnumerator is not null && _fallbackEnumerator.MoveNext())
                {
                    _current = _fallbackEnumerator.Current;
                    return true;
                }
                return false;
            }

            public void Dispose() => _fallbackEnumerator?.Dispose();
        }
    }

    // Pure type classification for Energy projection. Never read lifecycle getters here:
    // unknown neutral types survive projection until the actual dispatch guard.
    internal static bool IsKnownHookModelType(AbstractModel model) =>
        model is PowerModel or MonsterModel or OrbModel or CardModel or RelicModel
            or PotionModel or AfflictionModel or EnchantmentModel or RunModel;

    private static AbstractModel[] GetCostHookListeners(ICombatState combatState, HookOverrideIndex.CostSlot slot)
    {
        // The original gate is queried freshly at each phase, outside persistent cache data.
        if (combatState is CombatState concrete)
        {
            if (combatState.IsOverOrEnding() && !combatState.IsStarting()) return [];
            CombatState.HookListenerPhase phase = slot switch
            {
                HookOverrideIndex.CostSlot.Energy => CombatState.HookListenerPhase.Energy,
                HookOverrideIndex.CostSlot.EnergyLate => CombatState.HookListenerPhase.EnergyLate,
                _ => CombatState.HookListenerPhase.Star,
            };
            if (concrete.GetHookListenerSnapshot(combatState.RunState, phase) is { } snapshot)
                return snapshot.CostListeners(slot);
            return combatState.IterateHookListeners()
                .Where(model => (slot != HookOverrideIndex.CostSlot.Star && !IsKnownHookModelType(model))
                    || HookOverrideIndex.MayOverride(model, slot)).ToArray();
        }
        return IterateCombatHookListeners(combatState)
            .Where(model => (slot != HookOverrideIndex.CostSlot.Star && !IsKnownHookModelType(model))
                    || HookOverrideIndex.MayOverride(model, slot)).ToArray();
    }

    public static async Task AfterOrbChanneled(ICombatState combatState, Player player, OrbModel orb)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterOrbChanneled, notifyExecFinished: true))
        {
            await model.AfterOrbChanneled(player, orb);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterOrbEvoked(
        ICombatState combatState, OrbModel orb, IEnumerable<Creature> targets)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterOrbEvoked, notifyExecFinished: true))
        {
            await model.AfterOrbEvoked(orb, targets);
            model.InvokeExecutionFinished();
        }
    }

    public static int ModifyOrbPassiveTriggerCount(
        ICombatState combatState,
        OrbModel orb,
        int triggerCount,
        out IReadOnlyList<AbstractModel> modifyingModels)
    {
        var changed = new List<AbstractModel>();
        int result = triggerCount;
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyOrbPassiveTriggerCounts))
        {
            int before = result;
            result = model.ModifyOrbPassiveTriggerCounts(orb, result);
            if (result != before)
                changed.Add(model);
        }
        modifyingModels = changed;
        return result;
    }

    public static async Task AfterModifyingOrbPassiveTriggerCount(
        ICombatState combatState,
        OrbModel orb,
        IEnumerable<AbstractModel> modifiers)
    {
        if (HasNoKnownModifiersAndCleanCombat(combatState, modifiers)) return;
        foreach (AbstractModel model in CombatRaw(combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!modifiers.Contains(model))
                continue;
            await model.AfterModifyingOrbPassiveTriggerCount(orb);
            model.InvokeExecutionFinished();
        }
    }

    public static decimal ModifyOrbValue(ICombatState combatState, OrbModel orb, decimal amount)
    {
        decimal result = amount;
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyOrbValue))
        {
            result = model.ModifyOrbValue(orb, result);
        }
        return result;
    }

    public static async Task BeforeCombatStart(ICombatState combatState)
    {
        foreach (AbstractModel model in RunRaw(combatState.RunState, combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.BeforeCombatStart();
            model.InvokeExecutionFinished();
        }
        foreach (AbstractModel model in RunRaw(combatState.RunState, combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.BeforeCombatStartLate();
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldDisableRemainingRestSiteOptions(IRunState runState, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!model.ShouldDisableRemainingRestSiteOptions(player)) return false;
        }
        return true;
    }

    public static async Task AfterCreatureAddedToCombat(ICombatState combatState, Creature creature)
    {
        foreach (AbstractModel model in CombatRaw(combatState, gated: false, snapshotFallback: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterCreatureAddedToCombat(creature);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCombatVictory(ICombatState combatState)
    {
        foreach (AbstractModel model in RunRaw(combatState.RunState, combatState, eagerBothPhases: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterCombatVictoryEarly();
            model.InvokeExecutionFinished();
        }
        foreach (AbstractModel model in RunRaw(combatState.RunState, combatState, eagerBothPhases: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterCombatVictory();
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCombatEnd(ICombatState combatState)
    {
        foreach (AbstractModel model in RunRaw(combatState.RunState, combatState, eagerBothPhases: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterCombatEnd();
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldTakeExtraTurn(ICombatState combatState, Player player, out AbstractModel? source)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldTakeExtraTurnEarly))
        {
            if (model.ShouldTakeExtraTurnEarly(player))
            {
                source = model;
                return true;
            }
        }

        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldTakeExtraTurn))
        {
            if (model.ShouldTakeExtraTurn(player))
            {
                source = model;
                return true;
            }
        }

        source = null;
        return false;
    }

    public static async Task AfterTakingExtraTurn(ICombatState combatState, Player player, AbstractModel source)
    {
        foreach (AbstractModel model in CombatRaw(combatState, snapshotFallback: true))
        {
            if (!IsEligibleHookListener(model) || !ReferenceEquals(model, source)) continue;
            await model.AfterTakingExtraTurn(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeSideTurnStart(
        ICombatState combatState,
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeSideTurnStart, notifyExecFinished: true))
        {
            await model.BeforeSideTurnStart(side, participants);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterPlayerTurnStart(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterPlayerTurnStartEarly, notifyExecFinished: true))
        {
            await model.AfterPlayerTurnStartEarly(player);
            model.InvokeExecutionFinished();
        }
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterPlayerTurnStart, notifyExecFinished: true))
        {
            await model.AfterPlayerTurnStart(player);
            model.InvokeExecutionFinished();
        }
        await AfterPlayerTurnStartLate(combatState, player);
    }

    public static async Task AfterPlayerTurnStartLate(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterPlayerTurnStartLate, notifyExecFinished: true))
        {
            await model.AfterPlayerTurnStartLate(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterSideTurnStart(
        ICombatState combatState,
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterSideTurnStart, notifyExecFinished: true))
        {
            await model.AfterSideTurnStart(side, participants);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterSideTurnStartLate, notifyExecFinished: true))
        {
            await model.AfterSideTurnStartLate(side, participants);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterAutoPrePlayPhaseEntered(
        ICombatState combatState,
        Player player)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterAutoPrePlayPhaseEnteredEarly, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterAutoPrePlayPhaseEnteredEarly(player);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterAutoPrePlayPhaseEntered, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterAutoPrePlayPhaseEntered(player);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterAutoPrePlayPhaseEnteredLate, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterAutoPrePlayPhaseEnteredLate(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterAutoPostPlayPhaseEntered(
        ICombatState combatState,
        Player player)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterAutoPostPlayPhaseEntered, notifyExecFinished: true))
        {
            await model.AfterAutoPostPlayPhaseEntered(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeSideTurnEndVeryEarly(
        ICombatState combatState, CombatSide side, IEnumerable<Creature> participants)
    {
        ICombatObserver? observer = (combatState as CombatState)?.Observer;
        if (observer is null)
        {
            foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeSideTurnEndVeryEarly, notifyExecFinished: true))
            {
                await model.BeforeSideTurnEndVeryEarly(side, participants);
                model.InvokeExecutionFinished();
            }
            return;
        }

        foreach (AbstractModel model in CombatRaw(combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            observer.SideTurnEndListener(nameof(BeforeSideTurnEndVeryEarly), model, side, true);
            await model.BeforeSideTurnEndVeryEarly(side, participants);
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(BeforeSideTurnEndVeryEarly), model, side, false);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeSideTurnEndEarly(
        ICombatState combatState,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        ICombatObserver? observer = (combatState as CombatState)?.Observer;
        if (observer is null)
        {
            foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeSideTurnEndEarly, notifyExecFinished: true))
            {
                await model.BeforeSideTurnEndEarly(side, participants);
                model.InvokeExecutionFinished();
            }
            return;
        }

        foreach (AbstractModel model in CombatRaw(combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            observer.SideTurnEndListener(nameof(BeforeSideTurnEndEarly), model, side, true);
            await model.BeforeSideTurnEndEarly(side, participants);
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(BeforeSideTurnEndEarly), model, side, false);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeSideTurnEnd(
        ICombatState combatState,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        ICombatObserver? observer = (combatState as CombatState)?.Observer;
        if (observer is null)
        {
            Creature[]? participantSnapshot = null;
            foreach (AbstractModel model in CombatSlot(
                         combatState, HookOverrideIndex.HookSlot.BeforeSideTurnEnd, notifyExecFinished: true, snapshotFallback: true))
            {
                participantSnapshot ??= participants as Creature[] ?? participants.ToArray();
                await model.BeforeSideTurnEnd(side, participantSnapshot);
                model.InvokeExecutionFinished();
            }
            return;
        }

        Creature[] snapshot = participants.ToArray();
        foreach (AbstractModel model in CombatRaw(combatState, snapshotFallback: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            observer.SideTurnEndListener(nameof(BeforeSideTurnEnd), model, side, true);
            await model.BeforeSideTurnEnd(side, snapshot);
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(BeforeSideTurnEnd), model, side, false);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterSideTurnEnd(
        ICombatState combatState,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if ((combatState as CombatState)?.Observer is null)
        {
            foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterSideTurnEnd, notifyExecFinished: true))
            {
                await model.AfterSideTurnEnd(side, participants);
                model.InvokeExecutionFinished();
            }

            if ((combatState as CombatState)?.Observer is null)
            {
                foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterSideTurnEndLate, notifyExecFinished: true))
                {
                    await model.AfterSideTurnEndLate(side, participants);
                    model.InvokeExecutionFinished();
                }
                return;
            }
        }
        else
        {
            foreach (AbstractModel model in CombatRaw(combatState))
            {
                if (!IsEligibleHookListener(model)) continue;
                (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(AfterSideTurnEnd), model, side, true);
                await model.AfterSideTurnEnd(side, participants);
                (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(AfterSideTurnEnd), model, side, false);
                model.InvokeExecutionFinished();
            }
        }

        foreach (AbstractModel model in CombatRaw(combatState))
        {
            if ((combatState as CombatState)?.Observer is null && SkipNoOp(model, HookOverrideIndex.HookSlot.AfterSideTurnEndLate)) continue;
            if (!IsEligibleHookListener(model)) continue;
            (combatState as CombatState)?.Observer?.SideTurnEndListener("AfterSideTurnEndLate", model, side, true);
            await model.AfterSideTurnEndLate(side, participants);
            (combatState as CombatState)?.Observer?.SideTurnEndListener("AfterSideTurnEndLate", model, side, false);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterEnergyReset(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterEnergyReset, notifyExecFinished: true))
        {
            await model.AfterEnergyReset(player);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterEnergyResetLate, notifyExecFinished: true))
        {
            await model.AfterEnergyResetLate(player);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldPlayerResetEnergy(ICombatState combatState, Player player)
    {
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldPlayerResetEnergy))
        {
            if (!item.ShouldPlayerResetEnergy(player))
            {
                return false;
            }
        }

        return true;
    }

    public static decimal ModifyMaxEnergy(ICombatState combatState, Player player, decimal amount)
    {
        decimal num = amount;
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyMaxEnergy))
        {
            num = item.ModifyMaxEnergy(player, num);
        }

        return num;
    }

    public static int ModifyXValue(
        ICombatState combatState,
        CardModel card,
        int originalValue)
    {
        int result = originalValue;
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyXValue))
        {
            result = model.ModifyXValue(card, result);
        }

        return result;
    }

    public static async Task BeforeHandDraw(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeHandDraw, notifyExecFinished: true))
        {
            await model.BeforeHandDraw(player);
            model.InvokeExecutionFinished();
        }
    }

    public static decimal ModifyHandDraw(ICombatState combatState, Player player, decimal originalCardCount) =>
        ModifyHandDraw(combatState, player, originalCardCount, out _);

    public static decimal ModifyHandDraw(ICombatState combatState, Player player, decimal originalCardCount,
        out IEnumerable<AbstractModel> modifiers)
    {
        decimal num = originalCardCount;
        List<AbstractModel> changed = new();
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyHandDraw))
        {
            decimal before = num;
            num = item.ModifyHandDraw(player, num);
            if ((int)before != (int)num) changed.Add(item);
        }

        modifiers = changed;
        return num;
    }

    public static async Task AfterModifyingHandDraw(ICombatState combatState, IEnumerable<AbstractModel> modifiers)
    {
        if (HasNoKnownModifiersAndCleanCombat(combatState, modifiers)) return;
        foreach (AbstractModel model in CombatRaw(combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!modifiers.Contains(model)) continue;
            if (!HookOverrideIndex.Skips(model, HookOverrideIndex.HookSlot.AfterModifyingHandDraw))
                await model.AfterModifyingHandDraw();
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldDraw(
        ICombatState combatState,
        Player player,
        bool fromHandDraw,
        out AbstractModel? preventer)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldDraw))
        {
            if (!model.ShouldDraw(player, fromHandDraw))
            {
                preventer = model;
                return false;
            }
        }

        preventer = null;
        return true;
    }

    public static async Task AfterCardDrawn(ICombatState combatState, CardModel card, bool fromHandDraw)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterCardDrawnEarly, notifyExecFinished: true))
        {
            await model.AfterCardDrawnEarly(card, fromHandDraw);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterCardDrawn, notifyExecFinished: true))
        {
            await model.AfterCardDrawn(card, fromHandDraw);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCardDiscarded(ICombatState combatState, CardModel card)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterCardDiscarded, notifyExecFinished: true))
        {
            await model.AfterCardDiscarded(card);
            model.InvokeExecutionFinished();
        }
    }

    public static Task AfterCardGenerated(ICombatState combatState, CardModel card) =>
        AfterCardGenerated(combatState, card, card.Owner);

    public static async Task AfterCardGenerated(
        ICombatState combatState,
        CardModel card,
        Player? creator)
    {
        foreach (AbstractModel model in CombatRaw(combatState, snapshotFallback: true))
        {
            await model.AfterCardGenerated(card, creator);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCardGeneratedForCombat(
        ICombatState combatState,
        CardModel card,
        Player? creator)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(card);
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterCardGeneratedForCombat, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterCardGeneratedForCombat(card, creator);
            model.InvokeExecutionFinished();
        }
    }

    public static Task AfterCardEnteredCombat(ICombatState combatState, CardModel card) =>
        AfterCardEnteredCombat(SnapshotCardEnteredCombatListeners(combatState), card, invokedListeners: null);

    internal static IReadOnlyList<AbstractModel> SnapshotCardEnteredCombatListeners(ICombatState combatState)
    {
        if (combatState is CombatState concrete)
        {
            if (concrete.IsOverOrEnding() && !concrete.IsStarting())
                return Array.Empty<AbstractModel>();
            return concrete.HookListenerList();
        }
        return IterateCombatHookListeners(combatState).ToArray();
    }

    internal static async Task AfterCardEnteredCombat(
        IReadOnlyList<AbstractModel> listeners,
        CardModel card,
        ICollection<AbstractModel>? invokedListeners)
    {
        for (int i = 0; i < listeners.Count; i++)
        {
            AbstractModel model = listeners[i];
            if (invokedListeners is null && SkipNoOp(model, HookOverrideIndex.HookSlot.AfterCardEnteredCombat))
                continue;
            if (!IsEligibleHookListener(model)) continue;
            invokedListeners?.Add(model);
            if (!HookOverrideIndex.Skips(model, HookOverrideIndex.HookSlot.AfterCardEnteredCombat))
                await model.AfterCardEnteredCombat(card);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCardOwnerChanged(
        ICombatState combatState,
        CardModel card,
        Player oldOwner)
    {
        foreach (AbstractModel model in CombatRaw(combatState, snapshotFallback: true))
        {
            await model.AfterCardOwnerChanged(card, oldOwner);
            model.InvokeExecutionFinished();
        }
    }

    internal static async Task AfterCardEntryAborted(
        IReadOnlyList<AbstractModel> listeners,
        CardModel card)
    {
        List<Exception> failures = new();
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            AbstractModel model = listeners[i];
            try
            {
                await model.AfterCardEntryAborted(card);
                model.InvokeExecutionFinished();
            }
            catch (DecisionSuspendedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // fail-open: collected and rethrown as one AggregateException after every compensation ran
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("One or more combat card entry compensations failed.", failures);
        }
    }

    /// <summary>原版 <c>Hook.ModifyKeywordsInCombat</c>：由 <see cref="CardModel.GetKeywordsWithSources"/> 在需要
    /// Global 关键字时调用，结果不写回卡牌。</summary>
    public static void ModifyKeywordsInCombat(ICombatState combatState, CardModel card, ISet<CardKeyword> keywords)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.TryModifyKeywordsInCombat, gated: false))
        {
            model.TryModifyKeywordsInCombat(card, keywords);
        }
    }

    public static decimal ModifyEnergyCostInCombat(
        ICombatState combatState,
        CardModel card,
        decimal originalCost) =>
        ModifyEnergyCostInCombat(combatState, card, originalCost, out _);

    public static decimal ModifyEnergyCostInCombat(
        ICombatState combatState,
        CardModel card,
        decimal originalCost,
        out bool wasModified)
    {
        decimal modifiedCost = originalCost;
        wasModified = false;
        foreach (AbstractModel model in GetCostHookListeners(combatState, HookOverrideIndex.CostSlot.Energy))
        {
            if (!IsEligibleHookListener(model)) continue;
            wasModified |= model.TryModifyEnergyCostInCombat(card, modifiedCost, out modifiedCost);
        }

        foreach (AbstractModel model in GetCostHookListeners(combatState, HookOverrideIndex.CostSlot.EnergyLate))
        {
            if (!IsEligibleHookListener(model)) continue;
            wasModified |= model.TryModifyEnergyCostInCombatLate(card, modifiedCost, out modifiedCost);
        }

        return modifiedCost;
    }

    public static decimal ModifyStarCostInCombat(
        ICombatState combatState,
        CardModel card,
        decimal originalCost) =>
        ModifyStarCostInCombat(combatState, card, originalCost, out _);

    public static decimal ModifyStarCostInCombat(
        ICombatState combatState,
        CardModel card,
        decimal originalCost,
        out bool wasModified)
    {
        decimal modifiedCost = originalCost;
        wasModified = false;
        foreach (AbstractModel model in GetCostHookListeners(combatState, HookOverrideIndex.CostSlot.Star))
        {
            wasModified |= model.TryModifyStarCostInCombat(card, modifiedCost, out modifiedCost);
        }

        return modifiedCost;
    }

    public static Task AfterCardChangedPiles(
        IRunState runState,
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy) =>
        AfterCardChangedPiles(runState, null, card, oldPileType, clonedBy);

    public static async Task AfterCardChangedPiles(
        IRunState runState,
        ICombatState? combatState,
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        // Both original phases are eager and ungated. Keep every finished notification,
        // including listeners with neutral base bodies, at its original ordered position.
        // With a cached combat snapshot the run's deck part (#1072) comes first, captured with it before any hook runs.
        CombatState.HookListenerSnapshot? normal = (combatState as CombatState)?
            .GetHookListenerSnapshot(runState, CombatState.HookListenerPhase.AfterPiles);
        List<AbstractModel>? deck = normal is null ? null : ((CombatState)combatState!).DeckHookListenerList(runState.Players);
        AbstractModel[] listeners = normal?.Full ?? runState.IterateHookListeners(combatState).ToArray();
        if (deck is not null)
        {
            foreach (AbstractModel model in deck)
            {
                if (!IsEligibleHookListener(model)) continue;
                await model.AfterCardChangedPiles(card, oldPileType, clonedBy);
                model.InvokeExecutionFinished();
            }
        }
        for (int i = 0; i < listeners.Length; i++)
        {
            AbstractModel model = listeners[i];
            if (!IsEligibleHookListener(model)) continue;
            if (normal is null || normal.OverridesPile(i, HookOverrideIndex.PileSlot.Normal))
                await model.AfterCardChangedPiles(card, oldPileType, clonedBy);
            model.InvokeExecutionFinished();
        }
        // Normal Hook/await/finished can change any source, so late always obtains a fresh phase.
        CombatState.HookListenerSnapshot? late = (combatState as CombatState)?
            .GetHookListenerSnapshot(runState, CombatState.HookListenerPhase.AfterPilesLate);
        deck = late is null ? null : ((CombatState)combatState!).DeckHookListenerList(runState.Players);
        listeners = late?.Full ?? runState.IterateHookListeners(combatState).ToArray();
        if (deck is not null)
        {
            foreach (AbstractModel model in deck)
            {
                if (!IsEligibleHookListener(model)) continue;
                await model.AfterCardChangedPilesLate(card, oldPileType, clonedBy);
                model.InvokeExecutionFinished();
            }
        }
        for (int i = 0; i < listeners.Length; i++)
        {
            AbstractModel model = listeners[i];
            if (!IsEligibleHookListener(model)) continue;
            if (late is null || late.OverridesPile(i, HookOverrideIndex.PileSlot.Late))
                await model.AfterCardChangedPilesLate(card, oldPileType, clonedBy);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldEtherealTrigger(ICombatState combatState, CardModel card)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldEtherealTrigger))
        {
            if (!model.ShouldEtherealTrigger(card)) return false;
        }
        return true;
    }

    public static async Task BeforeCardPlayed(ICombatState combatState, CardPlay cardPlay)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeCardPlayed, notifyExecFinished: true))
        {
            await model.BeforeCardPlayed(cardPlay);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldPlay(ICombatState combatState, CardModel card, bool isAutoPlay)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldPlay))
        {
            if (!model.ShouldPlay(card, isAutoPlay))
            {
                return false;
            }
        }

        return true;
    }

    public static int ModifyCardPlayCount(
        ICombatState combatState,
        CardModel card,
        int playCount,
        Creature? target,
        out List<AbstractModel> modifyingModels)
    {
        int result = playCount;
        modifyingModels = new List<AbstractModel>();
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyCardPlayCount))
        {
            int before = result;
            result = model.ModifyCardPlayCount(card, target, result);
            if (result != before)
            {
                modifyingModels.Add(model);
            }
        }

        return result;
    }

    public static async Task AfterModifyingCardPlayCount(
        ICombatState combatState,
        CardModel card,
        IEnumerable<AbstractModel> modifiers)
    {
        if (HasNoKnownModifiersAndCleanCombat(combatState, modifiers)) return;
        AbstractModel[] recordedModifiers = modifiers.ToArray();
        foreach (AbstractModel model in CombatRaw(combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!recordedModifiers.Contains(model)) continue;
            if (!HookOverrideIndex.Skips(model, HookOverrideIndex.HookSlot.AfterModifyingCardPlayCount))
                await model.AfterModifyingCardPlayCount(card);
            model.InvokeExecutionFinished();
        }
    }

    public static CardLocation ModifyCardPlayResultLocation(
        ICombatState combatState,
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location,
        out IReadOnlyList<AbstractModel> modifiers)
    {
        var changed = new List<AbstractModel>();
        CardLocation result = location;
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyCardPlayResultLocation))
        {
            CardLocation before = result;
            result = model.ModifyCardPlayResultLocation(card, isAutoPlay, resources, result);
            if (result != before)
            {
                changed.Add(model);
            }
        }

        modifiers = changed;
        return result;
    }

    public static async Task AfterModifyingCardPlayResultLocation(
        CardModel card,
        CardLocation location,
        IEnumerable<AbstractModel> modifiers)
    {
        if (modifiers is ICollection<AbstractModel> { Count: 0 }) return;
        foreach (AbstractModel model in modifiers.ToArray())
        {
            if (!HookOverrideIndex.Skips(model, HookOverrideIndex.HookSlot.AfterModifyingCardPlayResultLocation))
                await model.AfterModifyingCardPlayResultLocation(card, location);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCardPlayed(ICombatState combatState, CardPlay cardPlay)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterCardPlayed, gated: false, notifyExecFinished: true))
        {
            await model.AfterCardPlayed(cardPlay);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterCardPlayedLate, gated: false, notifyExecFinished: true))
        {
            await model.AfterCardPlayedLate(cardPlay);
            model.InvokeExecutionFinished();
        }
    }

    /// <summary>原版遍历战斗侧监听者且不经过结束守卫（<c>combatState.IterateHookListeners()</c>，逐个生物依次是能力、
    /// 遗物、药水、卡牌）。</summary>
    public static async Task AfterDiedToDoom(ICombatState combatState, IReadOnlyList<Creature> creatures)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterDiedToDoom, gated: false, notifyExecFinished: true))
        {
            await model.AfterDiedToDoom(creatures);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeFlush(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeFlush, notifyExecFinished: true))
        {
            await model.BeforeFlush(player);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeFlushLate, notifyExecFinished: true))
        {
            await model.BeforeFlushLate(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterFlush(ICombatState combatState, Player player,
        IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterFlush, notifyExecFinished: true))
        {
            await model.AfterFlush(player, flushedCards, retainedCards);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterForge(
        ICombatState combatState,
        decimal amount,
        Player forger,
        AbstractModel? source)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterForge, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterForge(amount, forger, source);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterShuffle(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterShuffle, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterShuffle(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterHandEmptied(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterHandEmptied, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterHandEmptied(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCardExhausted(
        ICombatState combatState,
        CardModel card,
        bool causedByEthereal)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterCardExhausted, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterCardExhausted(card, causedByEthereal);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterArrowDislodged(
        ICombatState combatState,
        ImpaledPower power,
        ArrowSlot arrow,
        CardModel? source)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterArrowDislodged, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterArrowDislodged(power, arrow, source);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterArrowImpaled(
        ICombatState combatState,
        ImpaledPower power,
        ArrowSlot arrow,
        CardModel? source)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterArrowImpaled, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterArrowImpaled(power, arrow, source);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterArrowShot(
        ICombatState combatState,
        Player owner,
        ArrowSlot? arrow,
        CardModel? source)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterArrowShot, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterArrowShot(owner, arrow, source);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeAttack(ICombatState combatState, AttackCommand command)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeAttack, notifyExecFinished: true))
        {
            await model.BeforeAttack(command);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterAttack(ICombatState combatState, AttackCommand command)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterAttack, notifyExecFinished: true))
        {
            await model.AfterAttack(command);
            model.InvokeExecutionFinished();
        }
    }

    public static int ModifyAttackHitCount(ICombatState combatState, AttackCommand command, int originalHitCount)
    {
        int hitCount = originalHitCount;
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyAttackHitCount))
        {
            hitCount = item.ModifyAttackHitCount(command, hitCount);
        }

        return hitCount;
    }

    public static Task BeforeDamageReceived(
        ICombatState combatState,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        BeforeDamageReceived(combatState.RunState, combatState, target, amount, props, dealer, cardSource);

    public static async Task BeforeDamageReceived(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        foreach (AbstractModel model in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.BeforeDamageReceived, notifyExecFinished: true))
        {
            await model.BeforeDamageReceived(target, amount, props, dealer, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    public static Task AfterDamageReceived(
        ICombatState combatState,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        AfterDamageReceived(combatState.RunState, combatState, target, result, props, dealer, cardSource);

    public static async Task AfterDamageReceived(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        foreach (AbstractModel model in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.AfterDamageReceived, notifyExecFinished: true))
        {
            await model.AfterDamageReceived(target, result, props, dealer, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    /// <summary>Dispatches the native HP-change hook in run listener order.</summary>
    public static async Task AfterCurrentHpChanged(
        IRunState runState, ICombatState? combatState, Creature creature, decimal delta)
    {
        foreach (AbstractModel model in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.AfterCurrentHpChanged, notifyExecFinished: true))
        {
            await model.AfterCurrentHpChanged(creature, delta);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterDamageGiven(
        ICombatState combatState,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterDamageGiven, gated: false, notifyExecFinished: true))
        {
            await model.AfterDamageGiven(dealer, result, props, target, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldDie(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        out AbstractModel? preventer)
    {
        foreach (AbstractModel model in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ShouldDie, eagerBothPhases: true))
        {
            if (!model.ShouldDie(target))
            {
                preventer = model;
                return false;
            }
        }
        return ShouldDieLate(runState, combatState, target, out preventer);
    }

    public static bool ShouldDieLate(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        out AbstractModel? preventer)
    {
        foreach (AbstractModel model in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ShouldDieLate, eagerBothPhases: true))
        {
            if (!model.ShouldDieLate(target))
            {
                preventer = model;
                return false;
            }
        }

        preventer = null;
        return true;
    }

    public static async Task AfterPreventingDeath(
        AbstractModel preventer,
        Creature target)
    {
        await preventer.AfterPreventingDeath(target);
        preventer.InvokeExecutionFinished();
    }

    public static async Task BeforeDeath(IRunState runState, ICombatState? combatState, Creature target)
    {
        foreach (AbstractModel model in RunSlot(
                     runState, combatState, HookOverrideIndex.HookSlot.BeforeDeath, eagerBothPhases: true, notifyExecFinished: true))
        {
            await model.BeforeDeath(target);
            model.InvokeExecutionFinished();
        }
    }

    public static Task AfterDeath(ICombatState combatState, Creature target) =>
        AfterDeath(combatState.RunState, combatState, target);

    public static async Task AfterDeath(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        bool wasRemovalPrevented = false,
        CardModel? source = null)
    {
        foreach (AbstractModel model in RunRaw(runState, combatState, eagerBothPhases: true))
        {
            if (!IsEligibleHookListener(model)) continue;
            await model.AfterDeath(target, source, wasRemovalPrevented);
            model.InvokeExecutionFinished();
        }
    }
    public static bool ShouldOwnerDeathTriggerFatal(
        IRunState runState,
        ICombatState? combatState,
        Creature owner)
    {
        foreach (AbstractModel model in RunRaw(runState, combatState, eagerBothPhases: true))
        {
            if (model is PowerModel power && ReferenceEquals(power.Owner, owner) &&
                !model.ShouldOwnerDeathTriggerFatal())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>原版 v0.114.0 <c>Hook.ShouldPowerBeRemovedOnDeath(power)</c>：持有者已不在战斗中（已移出）时直接返回 true，
    /// 不咨询任何监听者；否则遍历持有者所在战斗的监听者。</summary>
    public static bool ShouldPowerBeRemovedOnDeath(PowerModel power)
    {
        ArgumentNullException.ThrowIfNull(power);
        ICombatState? combatState = power.Owner.CombatState;
        if (combatState is null)
            return true;

        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.ShouldPowerBeRemovedOnDeath, gated: false, snapshotFallback: true))
        {
            if (!model.ShouldPowerBeRemovedOnDeath(power))
            {
                return false;
            }
        }

        return true;
    }

    public static decimal ModifyDamage(
        ICombatState combatState,
        Creature? target,
        Creature? dealer,
        decimal damage,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        out IEnumerable<AbstractModel> modifiers,
        ModifyDamageHookType modifyDamageHookType = ModifyDamageHookType.All) =>
        ModifyDamage(
            combatState.RunState,
            combatState,
            target,
            dealer,
            damage,
            props,
            cardSource,
            cardPlay,
            out modifiers,
            modifyDamageHookType);

    public static decimal ModifyDamage(
        IRunState runState,
        ICombatState? combatState,
        Creature? target,
        Creature? dealer,
        decimal damage,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        out IEnumerable<AbstractModel> modifiers,
        ModifyDamageHookType modifyDamageHookType = ModifyDamageHookType.All)
    {
        decimal result = damage;
        List<AbstractModel> changed = new();

        // game Hook.ModifyDamage: the card's own enchantment modifies the base damage first (additive, then
        // multiplicative), before the listener loops; it is not a listener and is not recorded in modifiers.
        if (cardSource?.EnchantmentList is { Count: > 0 } enchantments)
        {
            EnchantmentModel enchantment = enchantments[0];
            if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
            {
                result += enchantment.EnchantDamageAdditive(result, props);
            }

            if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
            {
                result *= enchantment.EnchantDamageMultiplicative(result, props);
            }
        }

        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
        {
            foreach (AbstractModel item in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ModifyDamageAdditive))
            {
                decimal delta = item.ModifyDamageAdditive(target, result, props, dealer, cardSource, cardPlay);
                result += delta;
                if (delta != 0m)
                {
                    changed.Add(item);
                }
            }
        }

        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            foreach (AbstractModel item in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ModifyDamageMultiplicative))
            {
                decimal factor = item.ModifyDamageMultiplicative(target, result, props, dealer, cardSource, cardPlay);
                result *= factor;
                if (factor != 1m)
                {
                    changed.Add(item);
                }
            }

            foreach (AbstractModel item in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ModifyDamageMultiplicativeLate))
            {
                decimal factor = item.ModifyDamageMultiplicativeLate(target, result, props, dealer, cardSource, cardPlay);
                result *= factor;
                if (factor != 1m) changed.Add(item);
            }
        }

        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Cap))
        {
            decimal cap = decimal.MaxValue;
            foreach (AbstractModel item in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ModifyDamageCap))
            {
                decimal itemCap = item.ModifyDamageCap(target, props, dealer, cardSource, cardPlay);
                if (itemCap < cap)
                {
                    cap = itemCap;
                    if (result > itemCap)
                    {
                        result = itemCap;
                        changed.Add(item);
                    }
                }
            }
        }

        modifiers = changed;
        return Math.Max(0m, result);
    }

    /// <summary>原版 <c>Hook.ModifyHpLost</c>：按阶段依次跑 BeforeOsty、BeforeOstyLate、AfterOsty、AfterOstyLate
    /// 四轮，每轮遍历全部监听者；整数部分被改变的监听者记入 <paramref name="modifiers"/>。</summary>
    public static decimal ModifyHpLost(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        HpLossHookPhase phases,
        out IEnumerable<AbstractModel> modifiers)
    {
        decimal result = amount;
        List<AbstractModel> changed = new();

        if (phases.HasFlag(HpLossHookPhase.BeforeOsty))
        {
            foreach (AbstractModel item in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ModifyHpLostBeforeOsty))
            {
                decimal before = result;
                result = item.ModifyHpLostBeforeOsty(target, result, props, dealer, cardSource);
                if (decimal.Truncate(before) != decimal.Truncate(result)) changed.Add(item);
            }
            foreach (AbstractModel item in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ModifyHpLostBeforeOstyLate))
            {
                decimal before = result;
                result = item.ModifyHpLostBeforeOstyLate(target, result, props, dealer, cardSource);
                if (decimal.Truncate(before) != decimal.Truncate(result)) changed.Add(item);
            }
        }

        if (phases.HasFlag(HpLossHookPhase.AfterOsty))
        {
            foreach (AbstractModel item in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ModifyHpLostAfterOsty))
            {
                decimal before = result;
                result = item.ModifyHpLostAfterOsty(target, result, props, dealer, cardSource);
                if (decimal.Truncate(before) != decimal.Truncate(result)) changed.Add(item);
            }
            foreach (AbstractModel item in RunSlot(runState, combatState, HookOverrideIndex.HookSlot.ModifyHpLostAfterOstyLate))
            {
                decimal before = result;
                result = item.ModifyHpLostAfterOstyLate(target, result, props, dealer, cardSource);
                if (decimal.Truncate(before) != decimal.Truncate(result)) changed.Add(item);
            }
        }

        modifiers = changed;
        return result;
    }

    /// <summary>原版 v0.114.0 <c>Hook.AfterModifyingDamageAmount</c>：按监听顺序通知本次 <c>ModifyDamage</c> 的修正者。
    /// 修正者为空时原版遍历后什么也不做，这里直接返回。</summary>
    public static async Task AfterModifyingDamageAmount(IRunState runState,
        ICombatState? combatState, CardModel? cardSource, IEnumerable<AbstractModel> modifiers)
    {
        if (modifiers is ICollection<AbstractModel> { Count: 0 })
            return;
        AbstractModel[] changed = modifiers as AbstractModel[] ?? modifiers.ToArray();
        if (changed.Length == 0)
            return;

        foreach (AbstractModel model in RunRaw(runState, combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!changed.Contains(model)) continue;
            await model.AfterModifyingDamageAmount(cardSource);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterModifyingHpLostBeforeOsty(IRunState runState,
        ICombatState? combatState, IEnumerable<AbstractModel> modifiers)
    {
        if (HasNoKnownModifiersAndCleanRunCombat(runState, combatState, modifiers)) return;
        foreach (AbstractModel model in RunRaw(runState, combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!modifiers.Contains(model)) continue;
            if (!HookOverrideIndex.Skips(model, HookOverrideIndex.HookSlot.AfterModifyingHpLostBeforeOsty))
                await model.AfterModifyingHpLostBeforeOsty();
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterModifyingHpLostAfterOsty(IRunState runState,
        ICombatState? combatState, IEnumerable<AbstractModel> modifiers)
    {
        if (HasNoKnownModifiersAndCleanRunCombat(runState, combatState, modifiers)) return;
        foreach (AbstractModel model in RunRaw(runState, combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!modifiers.Contains(model)) continue;
            if (!HookOverrideIndex.Skips(model, HookOverrideIndex.HookSlot.AfterModifyingHpLostAfterOsty))
                await model.AfterModifyingHpLostAfterOsty();
            model.InvokeExecutionFinished();
        }
    }

    /// <summary>原版遍历战斗侧监听者且不经过结束守卫（<c>combatState?.IterateHookListeners()</c>）；战斗外原样返回。</summary>
    public static Creature ModifyUnblockedDamageTarget(ICombatState? combatState, Creature originalTarget,
        decimal amount, ValueProp props, Creature? dealer)
    {
        if (combatState is null) return originalTarget;
        Creature target = originalTarget;
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyUnblockedDamageTarget, gated: false))
        {
            target = item.ModifyUnblockedDamageTarget(target, amount, props, dealer);
        }

        return target;
    }

    /// <summary>Direct damage-resolution dispatch, including the combat-ending window.</summary>
    public static Creature? ModifyPreBlockDamageTarget(ICombatState? combatState, Creature originalTarget,
        decimal amount, ValueProp props, Creature? dealer)
    {
        if (combatState is null) return null;
        Creature? target = null;
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyPreBlockDamageTarget, gated: false))
        {
            target = model.ModifyPreBlockDamageTarget(originalTarget, target, amount, props, dealer);
        }
        return target;
    }

    public static bool ShouldGainStars(ICombatState combatState, decimal amount, Player player)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldGainStars))
        {
            if (!model.ShouldGainStars(amount, player)) return false;
        }
        return true;
    }

    public static decimal ModifySummonAmount(ICombatState combatState, Player summoner, decimal amount,
        AbstractModel? source)
    {
        decimal result = amount;
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifySummonAmount))
        {
            result = item.ModifySummonAmount(summoner, result, source);
        }

        return result;
    }

    public static async Task AfterSummon(ICombatState combatState, Player summoner, decimal amount)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterSummon, notifyExecFinished: true))
        {
            await model.AfterSummon(summoner, amount);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterOstyRevived(ICombatState combatState, Creature osty)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterOstyRevived, notifyExecFinished: true))
        {
            await model.AfterOstyRevived(osty);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeBlockGained(
        ICombatState combatState,
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforeBlockGained, notifyExecFinished: true))
        {
            await model.BeforeBlockGained(creature, amount, props, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterBlockGained(
        ICombatState combatState,
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterBlockGained, notifyExecFinished: true))
        {
            await model.AfterBlockGained(creature, amount, props, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    public static decimal ModifyBlock(
        ICombatState combatState,
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        out IEnumerable<AbstractModel> modifiers)
    {
        decimal result = block;
        List<AbstractModel> changed = new();

        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyBlockAdditive))
        {
            decimal delta = item.ModifyBlockAdditive(target, result, props, cardSource, cardPlay);
            result += delta;
            if (delta != 0m)
            {
                changed.Add(item);
            }
        }

        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyBlockMultiplicative))
        {
            decimal factor = item.ModifyBlockMultiplicative(target, result, props, cardSource, cardPlay);
            result *= factor;
            if (factor != 1m)
            {
                changed.Add(item);
            }
        }

        modifiers = changed;
        return Math.Max(0m, result);
    }

    public static async Task AfterModifyingBlockAmount(
        ICombatState combatState,
        decimal modifiedAmount,
        CardModel? cardSource,
        CardPlay? cardPlay,
        IEnumerable<AbstractModel> modifiers)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(modifiers);
        if (HasNoKnownModifiersAndCleanCombat(combatState, modifiers)) return;
        AbstractModel[] recordedModifiers = modifiers.ToArray();
        foreach (AbstractModel modifier in CombatRaw(combatState))
        {
            if (!IsEligibleHookListener(modifier)) continue;
            if (!recordedModifiers.Contains(modifier)) continue;
            if (!HookOverrideIndex.Skips(modifier, HookOverrideIndex.HookSlot.AfterModifyingBlockAmount))
                await modifier.AfterModifyingBlockAmount(modifiedAmount, cardSource, cardPlay);
            modifier.InvokeExecutionFinished();
        }
    }

    /// <summary>加算 → 乘算两段折叠。逐字移植（对应游戏 ModifyPowerAmountGivenAdditive/Multiplicative）。</summary>
    public static decimal ModifyPowerAmountGiven(ICombatState combatState, PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource, out IEnumerable<AbstractModel> modifiers)
    {
        decimal num = amount;
        List<AbstractModel> list = new();
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyPowerAmountGivenAdditive))
        {
            decimal delta = item.ModifyPowerAmountGivenAdditive(power, giver, num, target, cardSource);
            num += delta;
            if (delta != 0m)
            {
                list.Add(item);
            }
        }
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ModifyPowerAmountGivenMultiplicative))
        {
            decimal factor = item.ModifyPowerAmountGivenMultiplicative(power, giver, num, target, cardSource);
            num *= factor;
            if (factor != 1m)
            {
                list.Add(item);
            }
        }
        modifiers = list;
        return num;
    }

    public static decimal ModifyPowerAmountReceived(ICombatState combatState, PowerModel canonicalPower, Creature target, decimal amount, Creature? giver, out IEnumerable<AbstractModel> modifiers)
    {
        decimal num = amount;
        List<AbstractModel> list = new();
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.TryModifyPowerAmountReceived))
        {
            if (item.TryModifyPowerAmountReceived(canonicalPower, target, num, giver, out decimal modifiedAmount))
            {
                num = modifiedAmount;
                list.Add(item);
            }
        }
        modifiers = list;
        return num;
    }

    public static async Task AfterModifyingPowerAmountReceived(
        ICombatState combatState,
        IEnumerable<AbstractModel> modifiers,
        PowerModel modifiedPower)
    {
        if (HasNoKnownModifiersAndCleanCombat(combatState, modifiers)) return;
        AbstractModel[] recordedModifiers = modifiers.ToArray();
        foreach (AbstractModel modifier in CombatRaw(combatState))
        {
            if (!IsEligibleHookListener(modifier)) continue;
            if (!recordedModifiers.Contains(modifier)) continue;
            if (!HookOverrideIndex.Skips(modifier, HookOverrideIndex.HookSlot.AfterModifyingPowerAmountReceived))
                await modifier.AfterModifyingPowerAmountReceived(modifiedPower);
            modifier.InvokeExecutionFinished();
        }
    }

    public static async Task BeforePowerAmountChanged(ICombatState combatState, PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.BeforePowerAmountChanged, notifyExecFinished: true))
        {
            await model.BeforePowerAmountChanged(power, amount, target, applier, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterPowerAmountChanged(ICombatState combatState, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        (combatState as CombatState)?.Observer?.PowerAmountChanged(power, amount, applier, cardSource);
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterPowerAmountChanged, notifyExecFinished: true))
        {
            await model.AfterPowerAmountChanged(power, amount, applier, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldClearBlock(ICombatState combatState, Creature creature) =>
        ShouldClearBlock(combatState, creature, out _);

    public static bool ShouldClearBlock(ICombatState combatState, Creature creature, out AbstractModel? preventer)
    {
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldClearBlock))
        {
            if (!item.ShouldClearBlock(creature))
            {
                preventer = item;
                return false;
            }
        }

        preventer = null;
        return true;
    }

    public static async Task AfterPreventingBlockClear(ICombatState combatState,
        AbstractModel preventer, Creature creature)
    {
        foreach (AbstractModel model in CombatRaw(combatState))
        {
            if (!IsEligibleHookListener(model)) continue;
            if (!ReferenceEquals(model, preventer)) continue;
            await preventer.AfterPreventingBlockClear(preventer, creature);
            preventer.InvokeExecutionFinished();
            return;
        }
    }

    public static async Task AfterBlockCleared(ICombatState combatState, Creature creature)
    {
        foreach (AbstractModel model in CombatSlot(combatState, HookOverrideIndex.HookSlot.AfterBlockCleared, notifyExecFinished: true))
        {
            await model.AfterBlockCleared(creature);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterBlockBroken(
        ICombatState combatState,
        Creature target,
        Creature? breaker)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.AfterBlockBroken, gated: false, notifyExecFinished: true, snapshotFallback: true))
        {
            await model.AfterBlockBroken(target, breaker);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldAllowHitting(ICombatState combatState, Creature creature)
    {
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldAllowHitting))
        {
            if (!item.ShouldAllowHitting(creature))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ShouldDisappearFromDoom(ICombatState combatState, Creature creature)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(creature);
        foreach (AbstractModel model in CombatRaw(combatState, snapshotFallback: true))
        {
            if (!model.ShouldDisappearFromDoom())
            {
                return false;
            }
        }

        return true;
    }

    public static bool ShouldCreatureBeRemovedFromCombatAfterDeath(ICombatState combatState, Creature creature)
    {
        foreach (AbstractModel model in CombatSlot(
                     combatState, HookOverrideIndex.HookSlot.ShouldCreatureBeRemovedFromCombatAfterDeath, gated: false, snapshotFallback: true))
        {
            if (!model.ShouldCreatureBeRemovedFromCombatAfterDeath(creature))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ShouldStopCombatFromEnding(ICombatState combatState)
    {
        foreach (AbstractModel item in CombatSlot(combatState, HookOverrideIndex.HookSlot.ShouldStopCombatFromEnding, gated: false))
        {
            if (item.ShouldStopCombatFromEnding())
            {
                return true;
            }
        }

        return false;
    }
}
