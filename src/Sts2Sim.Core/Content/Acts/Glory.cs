using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Content.Acts;

/// <summary>Act 3 Glory definition with its complete encounter, event, and Ancient pools.</summary>
public sealed class Glory : ActDefinition
{
    private static readonly IReadOnlyList<EncounterDefinition> AllEncounters =
        CreateAllEncounters();

    private static IReadOnlyList<EncounterDefinition> CreateAllEncounters()
    {
        var encounters = GloryEncounters.BatchA.Concat(GloryEncounters.BatchB).Concat(GloryEncounters.Bosses)
            .ToDictionary(encounter => encounter.Name);
        // Glory.GenerateAllEncounters order governs weighted draws; implementation batches do not.
        return
        [
            encounters["AxebotsNormal"],
            encounters["ConstructMenagerieNormal"],
            encounters["DevotedSculptorWeak"],
            encounters["AeonglassBoss"],
            encounters["FabricatorNormal"],
            encounters["FrogKnightNormal"],
            encounters["GlobeHeadNormal"],
            encounters["KnightsElite"],
            encounters["MechaKnightElite"],
            encounters["OwlMagistrateNormal"],
            encounters["QueenBoss"],
            encounters["ScrollsOfBitingNormal"],
            encounters["ScrollsOfBitingWeak"],
            encounters["SlimedBerserkerNormal"],
            encounters["SoulNexusElite"],
            encounters["TestSubjectBoss"],
            encounters["TheLostAndForgottenNormal"],
            encounters["TurretOperatorWeak"],
        ];
    }

    private static readonly IReadOnlySet<string> EliteNames = new HashSet<string>
    {
        "KnightsElite",
        "MechaKnightElite",
        "SoulNexusElite",
    };

    private static readonly IReadOnlySet<string> BossNames = new HashSet<string>
    {
        "AeonglassBoss",
        "QueenBoss",
        "TestSubjectBoss",
    };

    public override int Index => 2;

    public override IReadOnlyList<Type> EventPool => Act3EventPool.All;

    public override IReadOnlyList<Type> AncientPool { get; } =
        [typeof(Nonupeipe), typeof(Tanx), typeof(Vakuu)];

    public override int BaseNumberOfRooms => 13;

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
        int restCount = mapRng.NextInt(5, 7);
        int unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(mapRng) - 1;
        return new MapPointTypeCounts(unknownCount, restCount, ascension);
    }
}
