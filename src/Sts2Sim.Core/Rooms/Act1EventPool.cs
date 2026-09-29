using Sts2Sim.Core.Models.Events;

namespace Sts2Sim.Core.Rooms;

/// <summary>Canonical Act 1 (Overgrowth) event types; per-run ordering lives in <see cref="Runs.RunState"/>.</summary>
public static class Act1EventPool
{
    public static IReadOnlyList<Type> All { get; } = Array.AsReadOnly(new[]
    {
        typeof(AromaOfChaos),
        typeof(ByrdonisNest),
        typeof(DenseVegetation),
        typeof(JungleMazeAdventure),
        typeof(LuminousChoir),
        typeof(MorphicGrove),
        typeof(SapphireSeed),
        typeof(SunkenStatue),
        typeof(TabletOfTruth),
        typeof(UnrestSite),
        typeof(Wellspring),
        typeof(WhisperingHollow),
        typeof(WoodCarvings),
    });
}
