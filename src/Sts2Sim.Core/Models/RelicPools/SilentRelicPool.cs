using System.Collections.ObjectModel;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.RelicPools;

/// <summary>The official Silent relic pool in decompiled source order.</summary>
public sealed class SilentRelicPool : RelicPoolModel
{
    private static readonly Type[] OfficialRelicTypes =
    {
        typeof(HelicalDart), typeof(NinjaScroll), typeof(PaperKrane), typeof(RingOfTheSnake),
        typeof(SneckoSkull), typeof(Tingsha), typeof(ToughBandages), typeof(TwistedFunnel),
    };

    private static int _poolGeneration;
    private static ReadOnlyCollection<RelicModel>? _poolCache;

    public override IReadOnlyList<RelicModel> AllRelics =>
        ModelDb.ResolveRegisteredPool(OfficialRelicTypes, ref _poolGeneration, ref _poolCache);
}
