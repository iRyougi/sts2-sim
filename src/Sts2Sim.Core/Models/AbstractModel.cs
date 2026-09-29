using System.Runtime.CompilerServices;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Exceptions;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models;

/// <summary>
/// 全实体基类(卡牌/遗物/怪物/power/附魔/角色/…),移植自游戏同名类。
/// 实例二态:ModelDb 里的 canonical(只读模板)与进局后的 mutable(经 MutableClone)。
/// Hook virtual 方法与 Hooks.Hook 调度器一一对应,表面积随计划递增(偏离 #16)。
/// </summary>
public abstract class AbstractModel : IComparable<AbstractModel>
{
    public virtual bool ShouldFlush(Player player) => true;
    public virtual bool ShouldProcurePotion(PotionModel potion, Player player) => true;

    public virtual Map.ActMap ModifyGeneratedMap(IRunState runState, Map.ActMap map, int actIndex) => map;
    public virtual Map.ActMap ModifyGeneratedMapLate(IRunState runState, Map.ActMap map, int actIndex) => map;
    public virtual Task AfterMapGenerated(Map.ActMap map, int actIndex) => Task.CompletedTask;
    public virtual Task BeforeCardRemoved(CardModel card) => Task.CompletedTask;

    public virtual Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent) => Task.CompletedTask;

    public virtual bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
    {
        newCard = null;
        return false;
    }

    /// <summary>CardCreationResult's card payload is represented directly, as in reward option modifiers.</summary>
    public virtual void ModifyMerchantCardCreationResults(Player player, List<CardModel> cards) { }

    public ModelId Id { get; }

    public bool IsMutable { get; private set; }

    public bool IsCanonical => !IsMutable;

    public abstract bool ShouldReceiveCombatHooks { get; }

    public event Action<AbstractModel>? ExecutionFinished;

    protected AbstractModel()
    {
        Type type = GetType();
        if (ModelDb.Contains(type))
        {
            throw new DuplicateModelException(type);
        }
        Id = ModelDb.GetId(type);
    }

    public virtual int CompareTo(AbstractModel? other)
    {
        if (this == other)
        {
            return 0;
        }
        if (other == null)
        {
            return 1;
        }
        return Id.CompareTo(other.Id);
    }

    public void AssertMutable()
    {
        if (!IsMutable)
        {
            throw new CanonicalModelException(GetType());
        }
    }

    public void AssertCanonical()
    {
        if (IsMutable)
        {
            throw new MutableModelException(GetType());
        }
    }

    public AbstractModel ClonePreservingMutability()
    {
        if (!IsMutable)
        {
            return this;
        }
        return MutableClone();
    }

    public AbstractModel MutableClone()
    {
        AbstractModel abstractModel = (AbstractModel)MemberwiseClone();
        abstractModel.IsMutable = true;
        abstractModel.DeepCloneFields();
        abstractModel.AfterCloned();
        return abstractModel;
    }

    /// <summary>克隆钩子:子类深拷贝自己的引用类型字段。覆写必须调用 base.DeepCloneFields()(含可变性断言)。</summary>
    protected virtual void DeepCloneFields()
    {
        AssertMutable();
    }

    /// <summary>克隆收尾:清空 MemberwiseClone 带过来的 ExecutionFinished 订阅。覆写必须调用 base.AfterCloned(),否则克隆体会与原件共享事件订阅者。</summary>
    protected virtual void AfterCloned()
    {
        ExecutionFinished = null;
    }

    public void InvokeExecutionFinished()
    {
        ExecutionFinished?.Invoke(this);
    }

    public override string ToString()
    {
        return $"{Id} ({RuntimeHelpers.GetHashCode(this)})";
    }

    // ===== Hook 表面(Plan 02 子集,与 Hooks.Hook 一一对应;默认实现 = 不干预)=====

    public virtual Task AfterActEntered()
    {
        return Task.CompletedTask;
    }

    public virtual Task BeforeRoomEntered(Rooms.AbstractRoom room) => Task.CompletedTask;

    /// <summary>某个房间被进入后触发。逐字对照 <c>AbstractRoom.EnterInternal</c> 里 <c>Hook.AfterRoomEntered</c>
    /// 的调用点（Plan 04 第 34 个 hook）。</summary>
    public virtual Task AfterRoomEntered(Rooms.AbstractRoom room)
    {
        return Task.CompletedTask;
    }

    /// <summary>偏离 #115：新增休息点专用治疗量修饰切片，供 <c>RestSiteRoom</c> 在最终治疗前折叠。</summary>
    public virtual decimal ModifyRestSiteHealAmount(Creature creature, decimal amount)
    {
        return amount;
    }

    // 偏离 #323：保留并传递上游 isMimicked 区分；当前已移植监听器与上游一样不按该参数分支。
    public virtual Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        return Task.CompletedTask;
    }

    public virtual bool TryModifyRestSiteHealRewards(Player player, List<Rewards.Reward> rewards, bool isMimicked)
    {
        return false;
    }

    public virtual void ModifyAvailableRestSiteDecisions(
        IRunState runState,
        Player player,
        List<RestSiteDecision> decisions)
    {
    }

    public virtual IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes)
    {
        return roomTypes;
    }

    public virtual EventModel ModifyNextEvent(EventModel currentEvent) => currentEvent;

    public virtual float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float oddsIncrease)
    {
        return oddsIncrease;
    }

    // 偏离 #17：原版方法还接收 Player 参数；模拟器当时未引入该类型，Plan 03 后调整偏离登记。
    // Player 类型 Plan 03 才存在,届时恢复原签名并更新此偏离。
    public virtual bool ShouldForcePotionReward(RoomType roomType)
    {
        return false;
    }

    public virtual bool ShouldProceedToNextMapPoint()
    {
        return true;
    }

    public virtual bool ShouldAllowFreeTravel()
    {
        return false;
    }

    public virtual bool ShouldGenerateTreasure(Player player)
    {
        return true;
    }

    public virtual bool ShouldAllowAncient(IRunState runState, Player player, AncientEventModel ancientEvent)
    {
        return true;
    }

    public virtual Task BeforeCombatStartLate() => Task.CompletedTask;

    public virtual Task BeforeCombatStart()
    {
        return Task.CompletedTask;
    }

    /// <summary>Called after a creature is initialized and added during a live combat.</summary>
    public virtual Task AfterCreatureAddedToCombat(Creature creature)
    {
        return Task.CompletedTask;
    }

    public virtual bool ShouldDisableRemainingRestSiteOptions(Player player) => true;

    public virtual Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterPlayerTurnStartEarly(Player player) => Task.CompletedTask;

    public virtual Task AfterPlayerTurnStart(Player player) => Task.CompletedTask;

    public virtual Task AfterOrbChanneled(Player player, OrbModel orb) => Task.CompletedTask;

    public virtual Task AfterOrbEvoked(OrbModel orb, IEnumerable<Creature> targets) => Task.CompletedTask;

    public virtual Task AfterModifyingOrbPassiveTriggerCount(OrbModel orb) => Task.CompletedTask;

    public virtual int ModifyOrbPassiveTriggerCounts(OrbModel orb, int triggerCount) => triggerCount;

    public virtual decimal ModifyOrbValue(OrbModel orb, decimal value) => value;

    public virtual Task AfterPlayerTurnStartLate(Player player) => Task.CompletedTask;

    public virtual Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterSideTurnStartLate(CombatSide side, IReadOnlyList<Creature> participants)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterAutoPrePlayPhaseEntered(Player player)
    {
        return Task.CompletedTask;
    }

    /// <summary>Called after entering the player's automatic end-of-play phase, before
    /// the side-turn-end hooks and hand flush.</summary>
    public virtual Task AfterAutoPostPlayPhaseEntered(Player player) => Task.CompletedTask;

    public virtual Task BeforeSideTurnEndVeryEarly(CombatSide side, IEnumerable<Creature> participants)
    {
        return Task.CompletedTask;
    }

    public virtual Task BeforeSideTurnEndEarly(CombatSide side, IEnumerable<Creature> participants)
    {
        return Task.CompletedTask;
    }

    public virtual Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterEnergyReset(Player player)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterEnergySpent(CardModel card, int amount)
    {
        return Task.CompletedTask;
    }

    public virtual bool ShouldPlayerResetEnergy(Player player)
    {
        return true;
    }

    /// <summary>Whether a draw attempt should proceed. Hand draws can be exempted by listeners such as No Draw.</summary>
    public virtual bool ShouldDraw(Player player, bool fromHandDraw)
    {
        return true;
    }

    public virtual decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        return amount;
    }

    /// <summary>偏离 #134：新增最小共享 X 值折叠面，供能量 X 与星愿 X 的效果结算共同使用。</summary>
    public virtual int ModifyXValue(CardModel card, int originalValue) => originalValue;

    /// <summary>偏离 #135：洗牌完成后通知；省略真实选择上下文，只传入牌堆所属玩家。</summary>
    public virtual Task AfterShuffle(Player player) => Task.CompletedTask;

    public virtual Task BeforeHandDraw(Player player)
    {
        return Task.CompletedTask;
    }

    public virtual decimal ModifyHandDraw(Player player, decimal originalCardCount)
    {
        return originalCardCount;
    }

    public virtual Task AfterModifyingHandDraw() => Task.CompletedTask;

    /// <summary>Runs for each drawn card before the ordinary draw hook, matching the native two-pass dispatch.</summary>
    public virtual Task AfterCardDrawnEarly(CardModel card, bool fromHandDraw) => Task.CompletedTask;

    public virtual Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        return Task.CompletedTask;
    }

    /// <summary>Runs once for each card after it moves to the discard pile.</summary>
    public virtual Task AfterCardDiscarded(CardModel card)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterCardGenerated(CardModel card)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterCardGenerated(CardModel card, Player? creator)
    {
        return AfterCardGenerated(card);
    }

    /// <summary>一张牌被「生成」进本场战斗后触发（区别于
    /// <c>AfterCardEnteredCombat</c> 的入场时机）。Aeonglass 用它把累计的
    /// FakeUpgrade 次数补到之后才生成的 Wither 上。</summary>
    public virtual Task AfterCardGeneratedForCombat(CardModel card, Player? creator) => Task.CompletedTask;

    public virtual Task AfterCardEnteredCombat(CardModel card)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterCardOwnerChanged(CardModel card, Player oldOwner)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Compensates listener-owned state after a first-entry hook transaction aborts. Implementations must be
    /// idempotent. The command framework restores its pile and counter mutations, but cannot infer arbitrary
    /// external effects from listeners that do not implement this hook.
    /// </summary>
    public virtual Task AfterCardEntryAborted(CardModel card)
    {
        return Task.CompletedTask;
    }

    public virtual bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        return false;
    }

    public virtual bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        return false;
    }

    public virtual bool TryModifyStarCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        return false;
    }

    /// <summary>Notifies run-level listeners after a card permanently enters its owner's deck.</summary>
    public virtual Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        return Task.CompletedTask;
    }

    public virtual Task BeforeCardPlayed(CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }

    public virtual bool ShouldPlay(CardModel card, bool isAutoPlay) => true;

    public virtual int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        return playCount;
    }

    public virtual Task AfterModifyingCardPlayCount(CardModel card)
    {
        return Task.CompletedTask;
    }

    public virtual CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        return location;
    }

    public virtual Task AfterModifyingCardPlayResultLocation(CardModel card, CardLocation location)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterCardPlayed(CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterForge(decimal amount, Player forger, AbstractModel? source) => Task.CompletedTask;

    /// <summary>偏离 #129：本项目在显式异步手牌离开边界完成后分发“手牌变空”，不在同步通用牌堆移动中 sync-over-async，也不在回合末清手窗口分发。</summary>
    public virtual Task AfterHandEmptied(Player player) => Task.CompletedTask;

    /// <summary>偏离 #117：语义化耗尽通知；<paramref name="causedByEthereal"/> 区分回合末 Ethereal 耗尽。</summary>
    public virtual Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        return Task.CompletedTask;
    }

    public virtual Task BeforeAttack(AttackCommand command)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterAttack(AttackCommand command)
    {
        return Task.CompletedTask;
    }

    public virtual int ModifyAttackHitCount(AttackCommand command, int hitCount)
    {
        return hitCount;
    }

    public virtual Task BeforeDamageReceived(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    /// <summary>Called after a creature's current HP changes, before damage-given/received hooks.</summary>
    public virtual Task AfterCurrentHpChanged(Creature creature, decimal delta) => Task.CompletedTask;

    public virtual Task AfterDamageGiven(
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    public virtual bool ShouldDie(Creature creature) => true;

    public virtual bool ShouldDieLate(Creature creature)
    {
        return true;
    }

    public virtual Task AfterPreventingDeath(Creature creature)
    {
        return Task.CompletedTask;
    }

    public virtual Task BeforeDeath(Creature target) => Task.CompletedTask;

    /// <summary>Confirmed-death compatibility callback for listeners that reject prevented deaths.</summary>
    public virtual Task AfterDeath(Creature target) => Task.CompletedTask;

    /// <summary>Death notification, including a ShouldDie veto before its preventer restores HP.
    /// Animation and multiplayer choice context are omitted in the headless simulator.</summary>
    public virtual Task AfterDeath(Creature target, bool wasRemovalPrevented) =>
        wasRemovalPrevented ? Task.CompletedTask : AfterDeath(target);
    public virtual bool ShouldOwnerDeathTriggerFatal() => true;

    public virtual bool ShouldPowerBeRemovedOnDeath(PowerModel power) => true;

    public virtual decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return 0m;
    }

    public virtual decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return 1m;
    }

    public virtual decimal ModifyDamageCap(
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return decimal.MaxValue;
    }

    // 原版把未格挡伤害的修正分成 Osty 重定向前后两个阶段，各有一个 Late 轮次；
    // Hook.ModifyHpLost 按 HpLossHookPhase 依次调用，重定向发生在两个阶段之间。
    public virtual decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) => amount;

    public virtual decimal ModifyHpLostBeforeOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) => amount;

    public virtual decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) => amount;

    public virtual Task AfterModifyingHpLostBeforeOsty() => Task.CompletedTask;

    /// <summary>决定未格挡伤害由谁承受；原版只有 Osty 的 DieForYouPower 会把主人换成 Osty。</summary>
    public virtual Creature ModifyUnblockedDamageTarget(Creature target, decimal amount, ValueProp props,
        Creature? dealer) => target;

    public virtual decimal ModifySummonAmount(Player summoner, decimal amount, AbstractModel? source) => amount;

    public virtual Task AfterSummon(Player summoner, decimal amount) => Task.CompletedTask;

    public virtual Task AfterOstyRevived(Creature osty) => Task.CompletedTask;

    public virtual Task AfterDiedToDoom(IReadOnlyList<Creature> creatures) => Task.CompletedTask;

    /// <summary>玩家回合结束第一阶段的末尾、弃手牌之前；原版先跑一轮 BeforeFlush 再跑一轮 BeforeFlushLate。</summary>
    public virtual Task BeforeFlush(Player player) => Task.CompletedTask;

    public virtual Task BeforeFlushLate(Player player) => Task.CompletedTask;

    public virtual Task AfterFlush(Player player, IReadOnlyCollection<CardModel> flushedCards,
        IReadOnlyCollection<CardModel> retainedCards) => Task.CompletedTask;

    public virtual Task AfterCardPlayedLate(CardPlay cardPlay) => Task.CompletedTask;

    public virtual Task AfterEnergyResetLate(Player player) => Task.CompletedTask;

    public virtual Task BeforeBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    public virtual decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) => amount;

    public virtual Task AfterModifyingHpLostAfterOsty() => Task.CompletedTask;

    public virtual Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterModifyingBlockAmount(
        decimal modifiedAmount,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return Task.CompletedTask;
    }
    public virtual decimal ModifyBlockAdditive(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return 0m;
    }

    public virtual decimal ModifyBlockMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return 1m;
    }

    public virtual bool ShouldClearBlock(Creature creature)
    {
        return true;
    }

    public virtual Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature) => Task.CompletedTask;

    public virtual Task AfterBlockCleared(Creature creature)
    {
        return Task.CompletedTask;
    }

    public virtual Task AfterBlockBroken(Creature target, Creature? breaker) => Task.CompletedTask;

    public virtual bool ShouldAllowHitting(Creature creature)
    {
        return true;
    }

    /// <summary>Power 的拥有者死亡后，这个 Power 是否应当被摘掉。
    /// 多阶段 Boss 靠返回 false 让驱动复活的 Power 活过死亡。</summary>
    public virtual bool ShouldPowerBeRemovedAfterOwnerDeath() => true;

    /// <summary>对应原版 <c>MonsterModel.ShouldDisappearFromDoom</c>，原版只用来选择 Doom 死亡动画；
    /// Doom 的击杀走 <c>CreatureCmd.Kill</c>，是否真的死亡由 ShouldDie 类钩子决定，与此无关。</summary>
    public virtual bool ShouldDisappearFromDoom() => true;

    public virtual bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) => true;

    public virtual bool ShouldStopCombatFromEnding()
    {
        return false;
    }

    // ===== Hook 表面(Plan 03 新增 C 组：power;默认实现 = 不干预)=====

    public virtual decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource) => 0m;

    public virtual decimal ModifyPowerAmountGivenMultiplicative(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource) => 1m;

    public virtual bool TryModifyPowerAmountReceived(PowerModel power, Creature target, decimal amount, Creature? giver, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        return false;
    }

    public virtual Task AfterModifyingPowerAmountReceived(PowerModel power) => Task.CompletedTask;

    public virtual Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource) => Task.CompletedTask;

    public virtual Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource) => Task.CompletedTask;

    // ===== Hook surface (Plan 06a: gold; default implementation = no-op) =====

    public virtual decimal ModifyGoldGained(Player player, decimal amount) => amount;

    public virtual Task AfterGoldGained(Player player) => Task.CompletedTask;

    /// <summary>偏离 #109：The real game exposes several finer-grained reward hooks. This simulator
    /// implements only the minimal slice needed to append complete extra rewards; modifying existing reward
    /// options and custom reward screens remain deferred.</summary>
    public virtual void ModifyRewards(Player player, List<Rewards.Reward> rewards, RoomType roomType)
    {
    }

    public virtual CardCreationOptions ModifyCardRewardCreationOptions(Player player, CardCreationOptions options) => options;

    public virtual CardCreationOptions ModifyCardRewardCreationOptionsLate(Player player, CardCreationOptions options) => options;

    public virtual bool TryModifyCardRewardOptions(Player player, List<CardModel> options, CardCreationOptions creationOptions) => false;

    public virtual bool TryModifyCardRewardOptionsLate(Player player, List<CardModel> options, CardCreationOptions creationOptions) => false;

    public virtual Task BeforeCombatRewardOffered(Rewards.RewardsSet rewards, CombatRoom room) => Task.CompletedTask;

    public virtual CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option)
    {
        return null;
    }

    public virtual CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option,
        CardCreationOptions creationOptions) => TryModifyCardRewardOptionLate(runState, player, option);

    public virtual void AfterModifyingCardRewardOptions(
        IRunState runState,
        Player player,
        IReadOnlyList<CardModel> options)
    {
    }

    /// <summary>偏离 #127：商店价格以当前货架条目和基础价为输入，在读取展示/购买价格时动态折叠。</summary>
    public virtual decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal originalPrice) => originalPrice;

    /// <summary>偏离 #128：是否在一次成功购买后为普通商品槽补入新条目；卡牌移除仍是一次性服务。</summary>
    public virtual bool ShouldRefillMerchantEntry(MerchantEntry entry, Player player) => false;

    public virtual Task BeforePotionUsed(PotionModel potion, Creature? target) =>
        BeforePotionUsed(potion, potion.Owner);

    public virtual Task BeforePotionUsed(PotionModel potion, Player player) => Task.CompletedTask;

    public virtual Task AfterPotionUsed(PotionModel potion, Player player) => Task.CompletedTask;
    public virtual Task AfterPotionProcured(PotionModel potion) => Task.CompletedTask;
    public virtual Task AfterPotionDiscarded(PotionModel potion) => Task.CompletedTask;
    public virtual bool TryModifyCardRewardAlternatives(Player player, Rewards.CardReward reward,
        List<Rewards.CardRewardAlternative> alternatives) => false;

    public virtual Task AfterStarsGained(int amount, Player gainer) => Task.CompletedTask;

    public virtual Task AfterStarsSpent(int amount, Player spender) => Task.CompletedTask;

    public virtual bool ShouldTakeExtraTurn(Player player) => false;

    public virtual Task AfterTakingExtraTurn(Player player) => Task.CompletedTask;

    public virtual Task AfterCombatVictoryEarly() => Task.CompletedTask;

    public virtual Task AfterCombatVictory() => Task.CompletedTask;

    /// <summary>Runs at combat-room exit while combat state is still available.</summary>
    public virtual Task AfterCombatEnd() => Task.CompletedTask;
}
