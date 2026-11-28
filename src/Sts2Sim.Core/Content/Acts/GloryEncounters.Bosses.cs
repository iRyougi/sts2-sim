namespace Sts2Sim.Core.Content.Acts;

using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Dread.Dominions;
using Sts2Sim.Core.Models.Monsters;

public static partial class GloryEncounters
{
    public static IReadOnlyList<EncounterDefinition> Bosses { get; } =
    [
        // Glory.GenerateAllEncounters order governs seeded draws; BossDiscoveryOrder is UI-only.
        new(() => [(Monster<Aeonglass>(), null)], null, false, "AeonglassBoss")
        {
            DominionFactory = () => ModelDb.Dominion<AeonglassDominion>(),
        },
        new(() => [(Monster<TorchHeadAmalgam>(), "amalgam"), (Monster<Queen>(), "queen")], null, false, "QueenBoss")
        {
            Slots = ["amalgam", "queen"],
            DominionFactory = () => ModelDb.Dominion<QueenDominion>(),
        },
        new(() => [(Monster<TestSubject>(), null)], null, false, "TestSubjectBoss")
        {
            DominionFactory = () => ModelDb.Dominion<TestSubjectDominion>(),
        },
    ];
}
