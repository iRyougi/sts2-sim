namespace Sts2Sim.Core.Content.Acts;

using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models.Monsters;

public static partial class GloryEncounters
{
    public static IReadOnlyList<EncounterDefinition> Bosses { get; } =
    [
        // Glory.GenerateAllEncounters order governs seeded draws; BossDiscoveryOrder is UI-only.
        new(() => [(Monster<Aeonglass>(), null)], null, false, "AeonglassBoss"),
        new(() => [(Monster<TorchHeadAmalgam>(), "amalgam"), (Monster<Queen>(), "queen")], null, false, "QueenBoss"),
        new(() => [(Monster<TestSubject>(), null)], null, false, "TestSubjectBoss"),
    ];
}
