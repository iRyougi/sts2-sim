using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Dread.Dominions;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Content.Acts;

public static partial class UnderdocksEncounters
{
    // GenerateAllEncounters filtered to bosses; BossDiscoveryOrder is a separate save-dependent list.
    public static IReadOnlyList<EncounterDefinition> Bosses { get; } =
    [
        new(() => [(CloneMonster<LagavulinMatriarch>(), null)], null, false, "LagavulinMatriarchBoss")
        {
            DominionFactory = () => ModelDb.Dominion<LagavulinMatriarchDominion>(),
        },
        new(() => [(CloneMonster<SoulFysh>(), null)], null, false, "SoulFyshBoss")
        {
            DominionFactory = () => ModelDb.Dominion<SoulFyshDominion>(),
        },
        new(() => [(CloneMonster<WaterfallGiant>(), null)], null, false, "WaterfallGiantBoss")
        {
            DominionFactory = () => ModelDb.Dominion<WaterfallGiantDominion>(),
        },
    ];
}
