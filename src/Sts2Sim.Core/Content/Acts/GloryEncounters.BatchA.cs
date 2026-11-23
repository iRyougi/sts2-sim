namespace Sts2Sim.Core.Content.Acts;

using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;

public static partial class GloryEncounters
{
    public static IReadOnlyList<EncounterDefinition> BatchA { get; } =
    [
        new(() => [(Monster<Axebot>(), "front")], null, false, "AxebotsNormal"),
        new(() => [(Monster<DevotedSculptor>(), null)], null, true, "DevotedSculptorWeak"),
        // 原版 FabricatorNormal.Slots：造出的机器人按槽位插在制造者两侧
        new(() => [(Monster<Fabricator>(), "fabricator")], null, false, "FabricatorNormal") { Slots = ["bot1", "bot2", "fabricator", "bot3", "bot4"] },
        new(() => [(Monster<FrogKnight>(), null)], null, false, "FrogKnightNormal"),
        new(() => [(Monster<GlobeHead>(), null)], null, false, "GlobeHeadNormal"),
        new(() => [(Monster<MechaKnight>(), null)], null, false, "MechaKnightElite"),
        new(() => [(Monster<OwlMagistrate>(), null)], null, false, "OwlMagistrateNormal"),
    ];

    private static T Monster<T>() where T : MonsterModel => ModelDb.Monster<T>();
}
