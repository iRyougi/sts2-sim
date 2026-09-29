using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Content.Acts;

/// <summary>Act 2 Hive definition with its complete encounter, event, and Ancient pools.</summary>
public sealed class Hive : ActDefinition
{
    private static readonly IReadOnlyList<EncounterDefinition> AllEncounters =
        CreateAllEncounters();

    private static IReadOnlyList<EncounterDefinition> CreateAllEncounters()
    {
        var encounters = HiveEncounters.BatchA.Concat(HiveEncounters.BatchB)
            .ToDictionary(encounter => encounter.Name);
        // Hive.GenerateAllEncounters order governs weighted draws; implementation batches do not.
        return
        [
            encounters["BowlbugsNormal"],
            encounters["BowlbugsWeak"],
            encounters["ChompersNormal"],
            encounters["DecimillipedeElite"],
            encounters["EntomancerElite"],
            encounters["ExoskeletonsNormal"],
            encounters["ExoskeletonsWeak"],
            encounters["HunterKillerNormal"],
            encounters["KaiserCrabBoss"],
            encounters["InfestedPrismsElite"],
            encounters["KnowledgeDemonBoss"],
            encounters["LouseProgenitorNormal"],
            encounters["MytesNormal"],
            encounters["OvicopterNormal"],
            encounters["SlumberingBeetleNormal"],
            encounters["SpinyToadNormal"],
            encounters["TheInsatiableBoss"],
            encounters["TheObscuraNormal"],
            encounters["ThievingHopperWeak"],
            encounters["TunnelerWeak"],
        ];
    }

    private static readonly IReadOnlySet<string> EliteNames = new HashSet<string>
    {
        "DecimillipedeElite",
        "EntomancerElite",
        "InfestedPrismsElite",
    };

    private static readonly IReadOnlySet<string> BossNames = new HashSet<string>
    {
        "TheInsatiableBoss",
        "KnowledgeDemonBoss",
        "KaiserCrabBoss",
    };

    public override int Index => 1;

    public override IReadOnlyList<Type> EventPool => Act2EventPool.All;

    public override IReadOnlyList<Type> AncientPool { get; } =
        [typeof(Orobas), typeof(Pael), typeof(Tezcatara)];

    public override int BaseNumberOfRooms => 14;

    public override int NumberOfWeakEncounters => 2;

    protected override IReadOnlyList<EncounterDefinition> MonsterEncounters { get; } =
        AllEncounters.Where(encounter =>
            !EliteNames.Contains(encounter.Name) && !BossNames.Contains(encounter.Name)).ToArray();

    protected override IReadOnlyList<EncounterDefinition> EliteEncounters { get; } =
        AllEncounters.Where(encounter => EliteNames.Contains(encounter.Name)).ToArray();

    protected override IReadOnlyList<EncounterDefinition> BossEncounters { get; } =
        AllEncounters.Where(encounter => BossNames.Contains(encounter.Name)).ToArray();

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng, AscensionManager ascension)
    {
        int restCount = mapRng.NextGaussianInt(6, 1, 6, 7);
        int unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(mapRng) - 1;
        return new MapPointTypeCounts(unknownCount, restCount, ascension);
    }
}
