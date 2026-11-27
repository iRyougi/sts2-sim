using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Content.Acts;

/// <summary>Underdocks Act 1 alternate definition.</summary>
public sealed class Underdocks : ActDefinition
{
    public override int Index => 0;

    public override bool IsDefault => false;

    public override IReadOnlyList<Type> EventPool => UnderdocksEventPool.All;

    private static readonly IReadOnlyList<Type> StaticAncientPool = [typeof(Neow)];
    private static readonly IReadOnlyList<EncounterDefinition> StaticMonsterEncounters =
        [.. UnderdocksEncounters.BatchA, .. UnderdocksEncounters.BatchB];

    public override IReadOnlyList<Type> AncientPool => StaticAncientPool;

    public override int BaseNumberOfRooms => 15;

    public override int NumberOfWeakEncounters => 3;

    protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => StaticMonsterEncounters;

    protected override IReadOnlyList<EncounterDefinition> EliteEncounters =>
        UnderdocksEncounters.Elites;

    // Deviation #300: omit ApplyActDiscoveryOrderModifications and its first-run encounter ordering.
    // The normal randomized bags and boss selection also apply to independent initial episodes.
    protected override IReadOnlyList<EncounterDefinition> BossEncounters =>
        UnderdocksEncounters.Bosses;

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng, AscensionManager ascension)
    {
        int restCount = mapRng.NextGaussianInt(7, 1, 6, 7);
        int unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(mapRng);
        return new MapPointTypeCounts(unknownCount, restCount, ascension);
    }
}
