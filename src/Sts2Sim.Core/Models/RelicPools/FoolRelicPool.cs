using System.Collections.ObjectModel;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.RelicPools;

/// <summary>v0.113.1（d09d48e5）Fool 专属遗物池（原版 <c>FoolRelicPool</c> 原生顺序 8 件）。</summary>
public sealed class FoolRelicPool : RelicPoolModel
{
    private static readonly Type[] OfficialRelicTypes =
    [
        typeof(BizarreLamp),
        typeof(ConductorsBaton),
        typeof(ExerciseMat),
        typeof(Fertilizer),
        typeof(Ocarina),
        typeof(SongBow),
        typeof(Twine),
        typeof(WhittlingKnife),
    ];

    private static int _poolGeneration;
    private static ReadOnlyCollection<RelicModel>? _poolCache;

    public override IReadOnlyList<RelicModel> AllRelics =>
        ModelDb.ResolveStrictPool(OfficialRelicTypes, ref _poolGeneration, ref _poolCache);
}
