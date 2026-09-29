using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models;

public enum EnchantmentStatus
{
    Normal,
    Disabled,
}

/// <summary>
/// 卡牌实例附魔的最小公共基类。
/// 偏离 #146：只移植 Sown/Slither/Glam 所需的 <see cref="OnPlay"/>、经继承战斗 hook 转发的
/// <see cref="OnDrawn"/>、<see cref="EnchantPlayCount"/> 和通用附魔资格/堆叠面；
/// 出牌顺序遵循真实 <c>CardModel.OnPlayWrapper</c>：卡牌效果后、<c>Hook.AfterCardPlayed</c> 前，
/// 摸牌则由 <c>Hook.AfterCardDrawn</c> 按“卡牌后紧跟附魔”的监听器顺序触发。
/// Plan08b-3f 已接入伤害 additive/multiplicative 与格挡转发；描述/预览、存档表面仍未实现。
/// </summary>
public abstract class EnchantmentModel : AbstractModel, ICombatStateDescriptionContributor
{
    private CardModel? _owner;

    public EnchantmentStatus Status { get; protected set; } = EnchantmentStatus.Normal;

    public decimal Magnitude { get; private set; }

    public CardModel Owner =>
        _owner ?? throw new InvalidOperationException("Enchantment is not attached to a card.");

    public override bool ShouldReceiveCombatHooks => _owner?.ShouldReceiveCombatHooks ?? false;

    public virtual bool IsStackable => false;

    public virtual bool ShouldStartAtBottomOfDrawPile => false;

    public void AssignMagnitude(decimal magnitude)
    {
        AssertMutable();
        Magnitude = magnitude;
    }

    internal void AssignOwnerCard(CardModel card)
    {
        AssertMutable();
        card.AssertMutable();
        if (_owner is not null)
        {
            throw new InvalidOperationException("Enchantment is already attached to a card.");
        }

        _owner = card;
    }

    public virtual Task OnPlay(CardModel card) => Task.CompletedTask;

    public virtual Task OnPlay(CardModel card, CardPlay cardPlay) => OnPlay(card);

    public virtual Task OnDrawn(CardModel card) => Task.CompletedTask;

    public virtual void OnAttached(CardModel card)
    {
    }

    public virtual decimal EnchantBlockAdditive(decimal amount) => 0m;

    public virtual decimal EnchantDamageAdditive(decimal amount, ValueProp props) => 0m;

    public virtual decimal EnchantDamageMultiplicative(decimal amount, ValueProp props) => 1m;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        ReferenceEquals(cardSource, _owner) ? EnchantDamageAdditive(amount, props) : 0m;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        ReferenceEquals(cardSource, _owner) ? EnchantDamageMultiplicative(amount, props) : 1m;

    public virtual bool CanEnchant(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.Type is CardType.Status or CardType.Curse or CardType.Quest)
        {
            return false;
        }

        EnchantmentModel? existing = card.Enchantments.FirstOrDefault();
        if (existing is not null &&
            (!IsStackable || existing.GetType() != GetType()))
        {
            return false;
        }

        return true;
    }

    public virtual int EnchantPlayCount(int originalPlayCount) => originalPlayCount;

    public virtual void ModifyShuffleOrder(Player player, List<CardModel> cards, bool isInitialShuffle)
    {
    }

    public sealed override decimal ModifyBlockAdditive(
        Creature target, decimal amount, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) =>
        ReferenceEquals(cardSource, _owner) ? EnchantBlockAdditive(amount) : 0m;

    public sealed override Task AfterCardDrawn(CardModel card, bool fromHandDraw) =>
        ReferenceEquals(card, _owner)
            ? OnDrawn(card)
            : Task.CompletedTask;

    internal void RestoreCombatCloneStateFrom(EnchantmentModel source)
    {
        Status = source.Status;
    }

    internal virtual void AppendCombatStateDescription(ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) { }

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) => AppendCombatStateDescription(ref builder, context);

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _owner = null;
    }
}
