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

    public override IReadOnlyList<RelicModel> AllRelics => OfficialRelicTypes
        .Where(ModelDb.Contains)
        .Select(type => (RelicModel)ModelDb.Get(type))
        .ToArray();
}
