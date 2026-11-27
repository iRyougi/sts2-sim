using System.Collections.ObjectModel;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.RelicPools;

/// <summary>The eight native Ironclad relics in v0.111.0 source order.</summary>
public sealed class IroncladRelicPool : RelicPoolModel
{
    private static readonly Type[] OfficialRelicTypes =
    [
        typeof(Brimstone), typeof(BurningBlood), typeof(CharonsAshes),
        typeof(DemonTongue), typeof(PaperPhrog), typeof(RedSkull),
        typeof(RuinedHelmet), typeof(SelfFormingClay),
    ];

    private static int _poolGeneration;
    private static ReadOnlyCollection<RelicModel>? _poolCache;

    public override IReadOnlyList<RelicModel> AllRelics =>
        ModelDb.ResolveStrictPool(OfficialRelicTypes, ref _poolGeneration, ref _poolCache);
}
