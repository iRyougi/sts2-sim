using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
            if (!model.ShouldFlush(player)) return false;
        return true;
    }

    public static bool ShouldProcurePotion(IRunState runState, ICombatState? combatState, PotionModel potion, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState))
            if (!model.ShouldProcurePotion(potion, player)) return false;
        return true;
    }

    public static async Task AfterItemPurchased(IRunState runState, Player player,
        MerchantEntry itemPurchased, int goldSpent)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            await model.AfterItemPurchased(player, itemPurchased, goldSpent);
            model.InvokeExecutionFinished();
        }
    }

    public static CardModel ModifyCardBeingAddedToDeck(IRunState runState, CardModel card)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
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
            model.ModifyMerchantCardCreationResults(player, cards);
        }
    }

    public static Map.ActMap ModifyGeneratedMap(IRunState runState, Map.ActMap map, int actIndex)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
            map = model.ModifyGeneratedMap(runState, map, actIndex);
        foreach (AbstractModel model in runState.IterateHookListeners(null))
            map = model.ModifyGeneratedMapLate(runState, map, actIndex);
        return map;
    }

    public static async Task AfterMapGenerated(IRunState runState, Map.ActMap map, int actIndex)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            await model.AfterMapGenerated(map, actIndex);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeCardRemoved(IRunState runState, CardModel card)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null).ToArray())
        {
            await model.BeforeCardRemoved(card);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterActEntered(IRunState runState)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            await model.AfterActEntered();
            model.InvokeExecutionFinished();
        }
    }

    /// <summary>Dispatches before room setup, while persistent deck cards can still transform.</summary>
    public static async Task BeforeRoomEntered(IRunState runState, Rooms.AbstractRoom room)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null).ToArray())
        {
            await model.BeforeRoomEntered(room);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterRoomEntered(IRunState runState, Rooms.AbstractRoom room)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
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
            result = model.ModifyRestSiteHealAmount(creature, result);
        }

        return result;
    }

    public static async Task AfterRestSiteHeal(IRunState runState, Player player, bool isMimicked = false)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
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
            model.ModifyAvailableRestSiteDecisions(runState, player, decisions);
        }
    }

    public static decimal ModifyGoldGained(IRunState runState, Player player, decimal amount)
    {
        decimal result = amount;
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
            result = item.ModifyGoldGained(player, result);
        }
        return result;
    }

    public static async Task AfterGoldGained(IRunState runState, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
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
            result = model.ModifyMerchantPrice(player, entry, result);
        }

        return result;
    }

    public static bool ShouldRefillMerchantEntry(
        IRunState runState,
        MerchantEntry entry,
        Player player) =>
        runState.IterateHookListeners(null)
            .Any(model => model.ShouldRefillMerchantEntry(entry, player));

    public static void ModifyRewards(IRunState runState, Player player, List<Reward> rewards, RoomType roomType)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(player.Creature.CombatState))
        {
            model.ModifyRewards(player, rewards, roomType);
        }
    }

    public static CardCreationOptions ModifyCardRewardCreationOptions(IRunState runState, Player player, CardCreationOptions options)
    {
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
            options = listener.ModifyCardRewardCreationOptions(player, options);
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
            options = listener.ModifyCardRewardCreationOptionsLate(player, options);
        return options;
    }

    public static async Task BeforeCombatRewardOffered(Rewards.RewardsSet rewards, IRunState runState, CombatRoom room)
    {
        foreach (AbstractModel listener in runState.IterateHookListeners(null))
        {
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
        foreach (AbstractModel model in runState.IterateHookListeners(combatState).ToArray())
        {
            await model.BeforePotionUsed(potion, target);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterPotionProcured(IRunState runState, ICombatState? combatState, PotionModel potion)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState).ToArray())
        {
            await model.AfterPotionProcured(potion);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterPotionDiscarded(IRunState runState, ICombatState? combatState, PotionModel potion)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState).ToArray())
        {
            await model.AfterPotionDiscarded(potion);
            model.InvokeExecutionFinished();
        }
    }

    public static void ModifyCardRewardAlternatives(IRunState runState, Player player,
        CardReward reward, List<CardRewardAlternative> alternatives)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
            model.TryModifyCardRewardAlternatives(player, reward, alternatives);
    }

    public static bool CanRerollCardReward(IRunState runState, Player player, CardReward reward)
    {
        bool canReroll = false;
        foreach (AbstractModel model in runState.IterateHookListeners(null))
            canReroll |= model.TryEnableCardRewardReroll(player, reward);
        return canReroll;
    }
    public static async Task AfterPotionUsed(IRunState runState, PotionModel potion, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            await model.AfterPotionUsed(potion, player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterStarsGained(ICombatState combatState, int amount, Player gainer)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterStarsGained(amount, gainer);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterStarsSpent(ICombatState combatState, int amount, Player spender)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterStarsSpent(amount, spender);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterEnergySpent(ICombatState combatState, CardModel card, int amount)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
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
            readOnlySet = item.ModifyUnknownMapPointRoomTypes(readOnlySet);
        }
        return readOnlySet;
    }

    public static EventModel ModifyNextEvent(IRunState runState, EventModel currentEvent)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
            currentEvent = model.ModifyNextEvent(currentEvent);
        return currentEvent;
    }

    public static float ModifyOddsIncreaseForUnrolledRoomType(IRunState runState, RoomType roomType, float oddsIncrease)
    {
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
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
            flag = flag || item.ShouldForcePotionReward(roomType);
        }
        return flag;
    }

    public static bool ShouldProceedToNextMapPoint(IRunState runState)
    {
        foreach (AbstractModel item in runState.IterateHookListeners(null))
        {
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
            flag = flag || item.ShouldAllowFreeTravel();
        }
        return flag;
    }

    // 偏离 #173：计划正文误写为 OR 折叠；权威源码采用 AND/veto 语义，任一监听器返回 false 即抑制该玩家的宝箱奖励。
    public static bool ShouldGenerateTreasure(IRunState runState, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
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
            result &= listener.ShouldAllowAncient(runState, player, ancientEvent);
        }
        return result;
    }

    /// <summary>
    /// 偏离 #28: 游戏原版签名为 (IRunState, ICombatState?),本计划战斗期 hook 只从 ICombatState 派生 IRunState。
    /// </summary>
    private static IEnumerable<AbstractModel> IterateCombatHookListeners(ICombatState combatState)
    {
        return combatState.RunState.IterateHookListeners(combatState);
    }

    public static async Task AfterOrbChanneled(ICombatState combatState, Player player, OrbModel orb)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterOrbChanneled(player, orb);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterOrbEvoked(
        ICombatState combatState, OrbModel orb, IEnumerable<Creature> targets)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            if (!modifiers.Contains(model))
                continue;
            await model.AfterModifyingOrbPassiveTriggerCount(orb);
            model.InvokeExecutionFinished();
        }
    }

    public static decimal ModifyOrbValue(ICombatState combatState, OrbModel orb, decimal amount)
    {
        decimal result = amount;
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
            result = model.ModifyOrbValue(orb, result);
        return result;
    }

    public static async Task BeforeCombatStart(ICombatState combatState)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.BeforeCombatStart();
            model.InvokeExecutionFinished();
        }
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.BeforeCombatStartLate();
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldDisableRemainingRestSiteOptions(IRunState runState, Player player)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null))
            if (!model.ShouldDisableRemainingRestSiteOptions(player)) return false;
        return true;
    }

    public static async Task AfterCreatureAddedToCombat(ICombatState combatState, Creature creature)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterCreatureAddedToCombat(creature);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCombatVictory(ICombatState combatState)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterCombatVictoryEarly();
            model.InvokeExecutionFinished();
        }
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterCombatVictory();
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCombatEnd(ICombatState combatState)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterCombatEnd();
            model.InvokeExecutionFinished();
        }

        CardModel[] persistentDeckCards = combatState.RunState.Players
            .SelectMany(player => player.Deck.Cards)
            .ToArray();
        foreach (CardModel card in persistentDeckCards)
        {
            await card.AfterCombatEnd();
            card.InvokeExecutionFinished();
        }
    }

    public static bool ShouldTakeExtraTurn(ICombatState combatState, Player player)
    {
        return IterateCombatHookListeners(combatState)
            .Any(model => model.ShouldTakeExtraTurn(player));
    }

    public static async Task AfterTakingExtraTurn(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterTakingExtraTurn(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeSideTurnStart(
        ICombatState combatState,
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.BeforeSideTurnStart(side, participants);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterPlayerTurnStart(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterPlayerTurnStartEarly(player);
            model.InvokeExecutionFinished();
        }
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterPlayerTurnStart(player);
            model.InvokeExecutionFinished();
        }
        await AfterPlayerTurnStartLate(combatState, player);
    }

    public static async Task AfterPlayerTurnStartLate(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterSideTurnStart(side, participants);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterSideTurnStartLate(side, participants);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterAutoPrePlayPhaseEntered(
        ICombatState combatState,
        Player player)
    {
        // Native dispatches each phase from a fresh listener enumeration. The local listener
        // wrapper snapshots each phase before callbacks, preserving its existing same-phase order.
        if (combatState.IsOverOrEnding()) return;
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterAutoPrePlayPhaseEnteredEarly(player);
            model.InvokeExecutionFinished();
        }

        if (combatState.IsOverOrEnding()) return;
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterAutoPrePlayPhaseEntered(player);
            model.InvokeExecutionFinished();
        }

        if (combatState.IsOverOrEnding()) return;
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterAutoPrePlayPhaseEnteredLate(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterAutoPostPlayPhaseEntered(
        ICombatState combatState,
        Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterAutoPostPlayPhaseEntered(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeSideTurnEndVeryEarly(
        ICombatState combatState, CombatSide side, IEnumerable<Creature> participants)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(BeforeSideTurnEndVeryEarly), model, side, true);
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(BeforeSideTurnEndEarly), model, side, true);
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
        AbstractModel[] listeners = IterateCombatHookListeners(combatState).ToArray();
        Creature[] participantSnapshot = participants.ToArray();
        foreach (AbstractModel model in listeners)
        {
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(BeforeSideTurnEnd), model, side, true);
            await model.BeforeSideTurnEnd(side, participantSnapshot);
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(BeforeSideTurnEnd), model, side, false);
            model.InvokeExecutionFinished();
        }

        // 手牌里的回合末效果不在这里结算：原版在 CombatManager.DoTurnEnd 里先把牌移入打出区再结算，
        // 见 CombatEngine.DoTurnEndAsync。
    }

    public static async Task AfterSideTurnEnd(
        ICombatState combatState,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(AfterSideTurnEnd), model, side, true);
            await model.AfterSideTurnEnd(side, participants);
            (combatState as CombatState)?.Observer?.SideTurnEndListener(nameof(AfterSideTurnEnd), model, side, false);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterEnergyReset(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterEnergyReset(player);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterEnergyResetLate(player);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldPlayerResetEnergy(ICombatState combatState, Player player)
    {
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            result = model.ModifyXValue(card, result);
        }

        return result;
    }

    public static async Task BeforeHandDraw(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            if (!modifiers.Contains(model)) continue;
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterCardDrawnEarly(card, fromHandDraw);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterCardDrawn(card, fromHandDraw);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCardDiscarded(ICombatState combatState, CardModel card)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterCardGeneratedForCombat(card, creator);
            model.InvokeExecutionFinished();
        }
    }

    public static Task AfterCardEnteredCombat(ICombatState combatState, CardModel card) =>
        AfterCardEnteredCombat(SnapshotCardEnteredCombatListeners(combatState), card, invokedListeners: null);

    internal static AbstractModel[] SnapshotCardEnteredCombatListeners(ICombatState combatState) =>
        IterateCombatHookListeners(combatState).ToArray();

    internal static async Task AfterCardEnteredCombat(
        IReadOnlyList<AbstractModel> listeners,
        CardModel card,
        ICollection<AbstractModel>? invokedListeners)
    {
        foreach (AbstractModel model in listeners)
        {
            invokedListeners?.Add(model);
            await model.AfterCardEnteredCombat(card);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCardOwnerChanged(
        ICombatState combatState,
        CardModel card,
        Player oldOwner)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
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
        foreach (AbstractModel model in listeners.Reverse())
        {
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            wasModified |= model.TryModifyEnergyCostInCombat(card, modifiedCost, out modifiedCost);
        }

        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            wasModified |= model.TryModifyStarCostInCombat(card, modifiedCost, out modifiedCost);
        }

        return modifiedCost;
    }

    public static async Task AfterCardChangedPiles(
        IRunState runState,
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(null).ToArray())
        {
            await model.AfterCardChangedPiles(card, oldPileType, clonedBy);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeCardPlayed(ICombatState combatState, CardPlay cardPlay)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.BeforeCardPlayed(cardPlay);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldPlay(ICombatState combatState, CardModel card, bool isAutoPlay)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        AbstractModel[] recordedModifiers = modifiers.ToArray();
        foreach (AbstractModel model in recordedModifiers)
        {
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in modifiers.ToArray())
        {
            await model.AfterModifyingCardPlayResultLocation(card, location);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterCardPlayed(ICombatState combatState, CardPlay cardPlay)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterCardPlayed(cardPlay);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterCardPlayedLate(cardPlay);
            model.InvokeExecutionFinished();
        }
    }

    /// <summary>原版遍历战斗侧监听者且不经过结束守卫（<c>combatState.IterateHookListeners()</c>，逐个生物依次是能力、
    /// 遗物、药水、卡牌）。模拟器的无参 <c>IterateHookListeners()</c> 不含遗物和药水，BookRepairKnife 要靠含背包的版本。</summary>
    public static async Task AfterDiedToDoom(ICombatState combatState, IReadOnlyList<Creature> creatures)
    {
        IEnumerable<AbstractModel> listeners = combatState is CombatState concrete
            ? concrete.IterateHookListeners(includePlayerInventory: true)
            : combatState.IterateHookListeners();
        foreach (AbstractModel model in listeners)
        {
            await model.AfterDiedToDoom(creatures);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeFlush(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.BeforeFlush(player);
            model.InvokeExecutionFinished();
        }

        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.BeforeFlushLate(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterFlush(ICombatState combatState, Player player,
        IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterForge(amount, forger, source);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterShuffle(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterShuffle(player);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterHandEmptied(ICombatState combatState, Player player)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterCardExhausted(card, causedByEthereal);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task BeforeAttack(ICombatState combatState, AttackCommand command)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.BeforeAttack(command);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterAttack(ICombatState combatState, AttackCommand command)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterAttack(command);
            model.InvokeExecutionFinished();
        }
    }

    public static int ModifyAttackHitCount(ICombatState combatState, AttackCommand command, int originalHitCount)
    {
        int hitCount = originalHitCount;
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in runState.IterateHookListeners(combatState))
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
        foreach (AbstractModel model in runState.IterateHookListeners(combatState))
        {
            await model.AfterDamageReceived(target, result, props, dealer, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    /// <summary>Dispatches the native HP-change hook in run listener order.</summary>
    public static async Task AfterCurrentHpChanged(
        IRunState runState, ICombatState? combatState, Creature creature, decimal delta)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in runState.IterateHookListeners(combatState).ToArray())
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
        foreach (AbstractModel model in runState.IterateHookListeners(combatState).ToArray())
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
        foreach (AbstractModel model in runState.IterateHookListeners(combatState).ToArray())
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
        bool wasRemovalPrevented = false)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState).ToArray())
        {
            await model.AfterDeath(target, wasRemovalPrevented);
            model.InvokeExecutionFinished();
        }
    }
    public static bool ShouldOwnerDeathTriggerFatal(
        IRunState runState,
        ICombatState? combatState,
        Creature owner)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState).ToArray())
        {
            if (model is PowerModel power && ReferenceEquals(power.Owner, owner) &&
                !model.ShouldOwnerDeathTriggerFatal())
            {
                return false;
            }
        }

        return true;
    }

    public static bool ShouldPowerBeRemovedOnDeath(ICombatState combatState, PowerModel power)
    {
        foreach (AbstractModel model in combatState.IterateHookListeners().ToArray())
        {
            if (!model.ShouldPowerBeRemovedOnDeath(power))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ShouldPowerBeRemovedAfterOwnerDeath(ICombatState combatState, PowerModel power)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(power);
        return power.ShouldPowerBeRemovedAfterOwnerDeath();
    }

    public static decimal ModifyDamage(
        ICombatState combatState,
        Creature? target,
        Creature? dealer,
        decimal damage,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        out IEnumerable<AbstractModel> modifiers) =>
        ModifyDamage(
            combatState.RunState,
            combatState,
            target,
            dealer,
            damage,
            props,
            cardSource,
            cardPlay,
            out modifiers);

    public static decimal ModifyDamage(
        IRunState runState,
        ICombatState? combatState,
        Creature? target,
        Creature? dealer,
        decimal damage,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        out IEnumerable<AbstractModel> modifiers)
    {
        decimal result = damage;
        List<AbstractModel> changed = new();

        foreach (AbstractModel item in runState.IterateHookListeners(combatState))
        {
            decimal delta = item.ModifyDamageAdditive(target, result, props, dealer, cardSource, cardPlay);
            result += delta;
            if (delta != 0m)
            {
                changed.Add(item);
            }
        }

        foreach (AbstractModel item in runState.IterateHookListeners(combatState))
        {
            decimal factor = item.ModifyDamageMultiplicative(target, result, props, dealer, cardSource, cardPlay);
            result *= factor;
            if (factor != 1m)
            {
                changed.Add(item);
            }
        }

        decimal cap = decimal.MaxValue;
        foreach (AbstractModel item in runState.IterateHookListeners(combatState))
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

        void Round(Func<AbstractModel, decimal, decimal> modify)
        {
            foreach (AbstractModel item in runState.IterateHookListeners(combatState))
            {
                decimal before = result;
                result = modify(item, result);
                if (decimal.Truncate(before) != decimal.Truncate(result))
                {
                    changed.Add(item);
                }
            }
        }

        if (phases.HasFlag(HpLossHookPhase.BeforeOsty))
        {
            Round((item, value) => item.ModifyHpLostBeforeOsty(target, value, props, dealer, cardSource));
            Round((item, value) => item.ModifyHpLostBeforeOstyLate(target, value, props, dealer, cardSource));
        }

        if (phases.HasFlag(HpLossHookPhase.AfterOsty))
        {
            Round((item, value) => item.ModifyHpLostAfterOsty(target, value, props, dealer, cardSource));
            Round((item, value) => item.ModifyHpLostAfterOstyLate(target, value, props, dealer, cardSource));
        }

        modifiers = changed;
        return result;
    }

    public static async Task AfterModifyingHpLostBeforeOsty(IRunState runState,
        ICombatState? combatState, IEnumerable<AbstractModel> modifiers)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState))
        {
            if (!modifiers.Contains(model)) continue;
            await model.AfterModifyingHpLostBeforeOsty();
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterModifyingHpLostAfterOsty(IRunState runState,
        ICombatState? combatState, IEnumerable<AbstractModel> modifiers)
    {
        foreach (AbstractModel model in runState.IterateHookListeners(combatState))
        {
            if (!modifiers.Contains(model)) continue;
            await model.AfterModifyingHpLostAfterOsty();
            model.InvokeExecutionFinished();
        }
    }

    /// <summary>原版遍历战斗侧监听者且不经过结束守卫（<c>combatState.IterateHookListeners()</c>）。</summary>
    public static Creature ModifyUnblockedDamageTarget(ICombatState combatState, Creature originalTarget,
        decimal amount, ValueProp props, Creature? dealer)
    {
        Creature target = originalTarget;
        foreach (AbstractModel item in combatState.IterateHookListeners())
        {
            target = item.ModifyUnblockedDamageTarget(target, amount, props, dealer);
        }

        return target;
    }

    public static decimal ModifySummonAmount(ICombatState combatState, Player summoner, decimal amount,
        AbstractModel? source)
    {
        decimal result = amount;
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
        {
            result = item.ModifySummonAmount(summoner, result, source);
        }

        return result;
    }

    public static async Task AfterSummon(ICombatState combatState, Player summoner, decimal amount)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterSummon(summoner, amount);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterOstyRevived(ICombatState combatState, Creature osty)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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

        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
        {
            decimal delta = item.ModifyBlockAdditive(target, result, props, cardSource, cardPlay);
            result += delta;
            if (delta != 0m)
            {
                changed.Add(item);
            }
        }

        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel modifier in modifiers.ToArray())
        {
            await modifier.AfterModifyingBlockAmount(modifiedAmount, cardSource, cardPlay);
            modifier.InvokeExecutionFinished();
        }
    }

    /// <summary>加算 → 乘算两段折叠。逐字移植（对应游戏 ModifyPowerAmountGivenAdditive/Multiplicative）。</summary>
    public static decimal ModifyPowerAmountGiven(ICombatState combatState, PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource, out IEnumerable<AbstractModel> modifiers)
    {
        decimal num = amount;
        List<AbstractModel> list = new();
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
        {
            decimal delta = item.ModifyPowerAmountGivenAdditive(power, giver, num, target, cardSource);
            num += delta;
            if (delta != 0m)
            {
                list.Add(item);
            }
        }
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel modifier in modifiers.ToArray())
        {
            await modifier.AfterModifyingPowerAmountReceived(modifiedPower);
            modifier.InvokeExecutionFinished();
        }
    }

    public static async Task BeforePowerAmountChanged(ICombatState combatState, PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.BeforePowerAmountChanged(power, amount, target, applier, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    public static async Task AfterPowerAmountChanged(ICombatState combatState, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        (combatState as CombatState)?.Observer?.PowerAmountChanged(power, amount, applier, cardSource);
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            await model.AfterPowerAmountChanged(power, amount, applier, cardSource);
            model.InvokeExecutionFinished();
        }
    }

    private static IEnumerable<AbstractModel> IterateBlockClearListeners(ICombatState combatState) =>
        !combatState.IsLiveCombat() ? Array.Empty<AbstractModel>() :
        combatState is CombatState concrete
            ? concrete.IterateHookListeners(includePlayerInventory: true)
            : combatState.IterateHookListeners();

    public static bool ShouldClearBlock(ICombatState combatState, Creature creature) =>
        ShouldClearBlock(combatState, creature, out _);

    public static bool ShouldClearBlock(ICombatState combatState, Creature creature, out AbstractModel? preventer)
    {
        foreach (AbstractModel item in IterateBlockClearListeners(combatState))
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
        if (!IterateBlockClearListeners(combatState).Contains(preventer)) return;
        await preventer.AfterPreventingBlockClear(preventer, creature);
        preventer.InvokeExecutionFinished();
    }

    public static async Task AfterBlockCleared(ICombatState combatState, Creature creature)
    {
        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
        {
            await model.AfterBlockBroken(target, breaker);
            model.InvokeExecutionFinished();
        }
    }

    public static bool ShouldAllowHitting(ICombatState combatState, Creature creature)
    {
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
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
        foreach (AbstractModel model in IterateCombatHookListeners(combatState).ToArray())
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
        foreach (AbstractModel model in combatState.IterateHookListeners().ToArray())
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
        foreach (AbstractModel item in IterateCombatHookListeners(combatState))
        {
            if (item.ShouldStopCombatFromEnding())
            {
                return true;
            }
        }

        return false;
    }
}
