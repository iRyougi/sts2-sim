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

    public override IReadOnlyList<RelicModel> AllRelics => OfficialRelicTypes
        .Select(type => (RelicModel)ModelDb.Get(type)).ToArray();
}
