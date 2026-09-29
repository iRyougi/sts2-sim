namespace Sts2Sim.Core.Content.Acts;

using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;

public static partial class HiveEncounters
{
    public static IReadOnlyList<EncounterDefinition> BatchB { get; } =
    [
        new(CreateChompers, [EncounterTag.Chomper], false, "ChompersNormal"),
        new(() => [(Monster<Crusher>(), "crusher"), (Monster<Rocket>(), "rocket")], null, false, "KaiserCrabBoss"),
        new(() => [(Monster<KnowledgeDemon>(), null)], null, false, "KnowledgeDemonBoss"),
        new(() => [(Monster<LouseProgenitor>(), null)], null, false, "LouseProgenitorNormal"),
        new(() => [(Monster<Myte>(), "first"), (Monster<Myte>(), "second")], null, false, "MytesNormal"),
        new(() => [(Monster<BowlbugRock>(), "first"), (Monster<BowlbugSilk>(), "second"),
            (Monster<SlumberingBeetle>(), "third")], [EncounterTag.Workers], false, "SlumberingBeetleNormal"),
        new(() => [(Monster<SpinyToad>(), null)], null, false, "SpinyToadNormal"),
        new(() => [(Monster<TheInsatiable>(), null)], null, false, "TheInsatiableBoss"),
        new(() => [(Monster<TheObscura>(), "obscura")], null, false, "TheObscuraNormal"),
        new(() => [(Monster<ThievingHopper>(), null)], [EncounterTag.Thieves], true, "ThievingHopperWeak"),
        new(() => [(Monster<Tunneler>(), null)], [EncounterTag.Burrower], true, "TunnelerWeak"),
    ];

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateChompers()
    {
        Chomper first = Monster<Chomper>();
        var second = (Chomper)Monster<Chomper>().MutableClone();
        second.ScreamFirst = true;
        return [(first, null), (second, null)];
    }
}
