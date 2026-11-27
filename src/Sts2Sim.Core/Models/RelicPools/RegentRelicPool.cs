using System.Collections.ObjectModel;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.RelicPools;

/// <summary>The official Regent relic pool in decompiled source order.</summary>
public sealed class RegentRelicPool : RelicPoolModel
{
    private static readonly Type[] OfficialRelicTypes =
    {
        typeof(DivineRight), typeof(FencingManual), typeof(GalacticDust), typeof(LunarPastry),
        typeof(MiniRegent), typeof(OrangeDough), typeof(Regalite), typeof(VitruvianMinion),
    };

    private static int _poolGeneration;
    private static ReadOnlyCollection<RelicModel>? _poolCache;

    public override IReadOnlyList<RelicModel> AllRelics =>
        ModelDb.ResolveRegisteredPool(OfficialRelicTypes, ref _poolGeneration, ref _poolCache);
}
