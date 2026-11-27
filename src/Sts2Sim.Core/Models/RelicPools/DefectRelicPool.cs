using System.Collections.ObjectModel;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.RelicPools;

/// <summary>Defect relics in v0.111.0 GenerateAllRelics order.</summary>
public sealed class DefectRelicPool : RelicPoolModel
{
    private static readonly Type[] OfficialRelicTypes =
    [
        typeof(CrackedCore), typeof(DataDisk), typeof(EmotionChip), typeof(GoldPlatedCables),
        typeof(PowerCell), typeof(Metronome), typeof(RunicCapacitor), typeof(SymbioticVirus),
    ];

    private static int _poolGeneration;
    private static ReadOnlyCollection<RelicModel>? _poolCache;

    public override IReadOnlyList<RelicModel> AllRelics =>
        ModelDb.ResolveStrictPool(OfficialRelicTypes, ref _poolGeneration, ref _poolCache);
}
