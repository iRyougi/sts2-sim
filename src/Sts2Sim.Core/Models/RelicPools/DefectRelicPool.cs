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

    public override IReadOnlyList<RelicModel> AllRelics => OfficialRelicTypes
        .Select(type => (RelicModel)ModelDb.Get(type)).ToArray();
}
