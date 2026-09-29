using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>Authoritative curse transformation pool in game source order.</summary>
public sealed class CurseCardPool : CardPoolModel
{
    public static CurseCardPool Instance { get; } = new();
    private static readonly Type[] CardTypes =
    [
        typeof(AscendersBane),
        typeof(BadLuck),
        typeof(Clumsy),
        typeof(CurseOfTheBell),
        typeof(Debt),
        typeof(Decay),
        typeof(Doubt),
        typeof(Enthralled),
        typeof(Folly),
        typeof(Greed),
        typeof(Guilty),
        typeof(Injury),
        typeof(Normality),
        typeof(PoorSleep),
        typeof(Regret),
        typeof(Shame),
        typeof(SporeMind),
        typeof(Writhe),
    ];
    public override IReadOnlyList<CardModel> AllCards => CardTypes.Where(ModelDb.Contains)
        .Select(type => (CardModel)ModelDb.Get(type)).ToArray();
}
