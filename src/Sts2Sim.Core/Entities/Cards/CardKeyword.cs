namespace Sts2Sim.Core.Entities.Cards;

/// <summary>
/// 逐字移植（<c>MegaCrit.Sts2.Core.Entities.Cards.CardKeyword</c>）。
/// 偏离 #85（已销案，Plan 08b-2）：<c>Sly</c> 已随静默猎手 HandTrick 补齐；
/// <c>Eternal</c> 也已由持久牌堆规则和 Eternal 卡牌使用，权威枚举现已完整。
/// </summary>
public enum CardKeyword
{
    None,
    Exhaust,
    Ethereal,
    Innate,
    Retain,
    Sly,
    Unplayable,
    Eternal,
}
