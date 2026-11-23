using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>
/// sls2：照原版 Vambrace 移植。第一次从卡牌得到格挡时记下这张牌（TriggeringCard），这张牌这一次打出结束（AfterCardPlayed）之前，
/// 它带来的格挡全部翻倍（包括附魔在打出时给的格挡，如灵巧），打出结束后才算本场用过。
/// 原来在第一次翻倍时就标记用过，同一张牌随后的附魔格挡不再翻倍：充能电池+（10）带灵巧 3 算成 10 × 2 + 3 = 23，原版 (10 + 3) × 2 = 26。
/// 原版在 AfterModifyingBlockAmount（修正后的格挡大于 0）里记 TriggeringCard；sts2-sim 没有这个钩子（偏离 #122），在乘算切片里返回 2 时记。
/// </summary>
public sealed class Vambrace : RelicModel
{
    private CardModel? _triggeringCard;
    private bool _blockGainedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeCombatStart()
    {
        _triggeringCard = null;
        _blockGainedThisCombat = false;
        return Task.CompletedTask;
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!props.HasFlag(ValueProp.Move) ||
            cardSource is null ||
            (_triggeringCard is not null && !ReferenceEquals(_triggeringCard, cardSource)) ||
            cardSource.Owner != Owner ||
            _blockGainedThisCombat)
        {
            return 1m;
        }

        // 只在真正打出时（cardPlay 不为空）记下触发的牌：观测编码器给手牌算格挡预览也会走到这里，
        // 预览照原版按翻倍显示，但不能改状态（否则记下的是被预览的牌，真正打出的牌反而不翻倍；SIMX0001 搜索评测里每步存读档后格挡 26 对原版 39）
        if (amount > 0m && cardPlay is not null)
        {
            _triggeringCard = cardSource;
        }
        return 2m;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && ReferenceEquals(cardPlay.Card, _triggeringCard) && !_blockGainedThisCombat)
        {
            _blockGainedThisCombat = true;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _triggeringCard = null;
        _blockGainedThisCombat = false;
        return Task.CompletedTask;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        if (source is Vambrace { _triggeringCard: { } original } && cardMap.TryGetValue(original, out CardModel? clone))
        {
            _triggeringCard = clone;
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_blockGainedThisCombat);
        context.AppendCardReferences(ref builder, [_triggeringCard]);
    }
}
