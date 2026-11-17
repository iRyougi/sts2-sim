using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Entities.Cards;

/// <summary>全部 22 张塔罗牌列表，按 v0.113.1 原生 <c>TarotCards.All</c> 顺序排列。</summary>
public static class TarotCards
{
    private static readonly Type[] AllTypes =
    [
        typeof(Judgement),
        typeof(TheFool),
        typeof(TheHighPriestess),
        typeof(TheEmperor),
        typeof(TheEmpress),
        typeof(TheHangedMan),
        typeof(TheHierophant),
        typeof(TheChariot),
        typeof(Justice),
        typeof(TheMagician),
        typeof(TheMoon),
        typeof(TheTower),
        typeof(Strength),
        typeof(Death),
        typeof(Temperance),
        typeof(TheDevil),
        typeof(TheHermit),
        typeof(TheLovers),
        typeof(TheStar),
        typeof(TheSun),
        typeof(TheWorld),
        typeof(WheelOfFortune),
    ];

    public static IReadOnlyList<CardModel> All => AllTypes
        .Select(type => (CardModel)ModelDb.Get(type))
        .ToArray();
}
