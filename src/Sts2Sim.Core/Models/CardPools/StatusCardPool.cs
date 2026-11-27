using System.Collections.ObjectModel;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>Authoritative status transformation pool in game source order.</summary>
public sealed class StatusCardPool : CardPoolModel
{
    public static StatusCardPool Instance { get; } = new();
    private static readonly Type[] CardTypes =
    [
        typeof(Beckon),
        typeof(Burn),
        typeof(Dazed),
        typeof(Debris),
        typeof(FranticEscape),
        typeof(Infection),
        typeof(Wither),
        typeof(Sadness),
        typeof(Slimed),
        typeof(Soot),
        typeof(Toxic),
        typeof(Cards.Void),
        typeof(Wound),
    ];
    private static int _poolGeneration;
    private static ReadOnlyCollection<CardModel>? _poolCache;

    public override IReadOnlyList<CardModel> AllCards =>
        ModelDb.ResolveRegisteredPool(CardTypes, ref _poolGeneration, ref _poolCache);
}
