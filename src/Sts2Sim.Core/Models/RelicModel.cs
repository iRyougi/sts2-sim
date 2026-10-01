using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models;

/// <summary>Base relic model. 偏离 #66 已销案：<c>IsStackable</c> 已实现且默认 false，
/// 同类可堆叠遗物的去重与计数也已接线；IsUsedUp、IsTradable、pickup effects、
/// melt/pet eligibility 与 MerchantCost 元数据均已实现。</summary>
public abstract class RelicModel : AbstractModel, ICombatStateDescriptionContributor
{
    public event Action<RelicModel, IEnumerable<Creature>>? Flashed;

    public void Flash() => Flash([Owner.Creature]);

    public void Flash(IEnumerable<Creature> targets) => Flashed?.Invoke(this, targets);

    protected override void AfterCloned()
    {
        base.AfterCloned();
        Flashed = null;
    }

    public abstract RelicRarity Rarity { get; }

    public virtual bool AddsPet => false;

    private bool _isWax;
    private bool _isMelted;

    public bool IsWax
    {
        get => _isWax;
        set
        {
            AssertMutable();
            _isWax = value;
        }
    }

    public bool IsMelted
    {
        get => _isMelted;
        internal set
        {
            AssertMutable();
            _isMelted = value;
        }
    }

    public bool IsTradable => !IsUsedUp && !HasUponPickupEffect && !IsMelted && !AddsPet &&
        Rarity is not (RelicRarity.Starter or RelicRarity.Event or RelicRarity.Ancient);

    /// <summary>偏离 #67（Plan06a 已登记，此处补齐）：真实源码在拾取瞬间触发的效果 hook。逐字移植 <c>MegaCrit.Sts2.Core.Models.RelicModel.AfterObtained</c>。</summary>
    public virtual Task AfterObtained() => Task.CompletedTask;

    public virtual Task AfterRemoved() => Task.CompletedTask;

    /// <summary>是否是“用完即失效”的限次遗物（如 LizardTail）。</summary>
    public virtual bool IsUsedUp => false;

    /// <summary>是否有“拾取瞬间生效”的效果（如 Strawberry）——本项目没有真实游戏那样的拾取预览 UI，单纯用来标记“这个遗物只需要 AfterObtained，没有持续 hook”，不影响结算本身。</summary>
    public virtual bool HasUponPickupEffect => false;

    /// <summary>是否允许多份堆叠（如 Circlet）。</summary>
    public virtual bool IsStackable => false;

    public int StackCount { get; private set; } = 1;

    public void IncrementStackCount()
    {
        AssertMutable();
        if (!IsStackable)
        {
            throw new InvalidOperationException($"{GetType().Name} is not stackable.");
        }

        StackCount++;
    }

    /// <summary>是否允许出现在商店角色遗物槽位。</summary>
    public virtual bool IsAllowedInShops => true;

    /// <summary>偏离 #108 已销案（Plan 08b-3c）：真实门控为 总楼层数小于 41
    /// （多人 38），不是按当前幕数判定。</summary>
    public virtual bool IsAllowed(Runs.IRunState runState) => true;

    /// <summary>逐字移植 <c>RelicModel.IsBeforeAct3TreasureChest</c>：
    /// 这些“投资型”遗物的收益取决于这局还剩多少路要走，过了第三幕宝箱再发等于发空气。</summary>
    protected static bool IsBeforeAct3TreasureChest(Runs.IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        int threshold = runState.Players.Count > 1 ? 38 : 41;
        return runState.TotalFloor < threshold;
    }

    public virtual bool IsAllowedAtNeow(Runs.IRunState runState) => IsAllowed(runState);

    /// <summary>偏离 #169：真实游戏的全局 RewardsCmd.OfferCustom 映射为当前 Ancient Event 的 pending reward queue。</summary>
    protected void OfferRewards(RewardsSet rewards)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        if (Owner.RunState.CurrentRoom is not EventRoom room ||
            !room.Event.TryOfferRewardsFromCurrentEvent(Owner.RunState, rewards))
        {
            throw new InvalidOperationException("Relic rewards can only be offered from the current event.");
        }
    }

    /// <summary>Base shop price for this relic's rarity. Starter, Event, and Ancient relics are not purchasable.</summary>
    public virtual int MerchantCost => Rarity switch
    {
        RelicRarity.Common => 175,
        RelicRarity.Uncommon => 225,
        RelicRarity.Rare => 275,
        RelicRarity.Shop => 200,
        RelicRarity.None => 1,
        _ => int.MaxValue,
    };

    public Player Owner { get; private set; } = null!;

    public override bool ShouldReceiveCombatHooks => true;

    public void AssignOwner(Player owner)
    {
        AssertMutable();
        Owner = owner;
    }

    internal virtual void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
    }

    internal virtual void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
    }

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        AppendCombatStateDescription(ref builder, context);
}
