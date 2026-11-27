using System.Collections.ObjectModel;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.RelicPools;

/// <summary>The eight native Necrobinder relics in v0.111.0 source order.</summary>
public sealed class NecrobinderRelicPool : RelicPoolModel
{
    private static readonly Type[] OfficialRelicTypes =
    [
        typeof(BigHat), typeof(BoneFlute), typeof(BookRepairKnife), typeof(Bookmark),
        typeof(BoundPhylactery), typeof(FuneraryMask), typeof(IvoryTile), typeof(UndyingSigil),
    ];

    private static int _poolGeneration;
    private static ReadOnlyCollection<RelicModel>? _poolCache;

    public override IReadOnlyList<RelicModel> AllRelics =>
        ModelDb.ResolveStrictPool(OfficialRelicTypes, ref _poolGeneration, ref _poolCache);
}
