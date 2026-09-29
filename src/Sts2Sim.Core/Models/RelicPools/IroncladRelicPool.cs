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

    public override IReadOnlyList<RelicModel> AllRelics => OfficialRelicTypes
        .Select(type => (RelicModel)ModelDb.Get(type)).ToArray();
}
