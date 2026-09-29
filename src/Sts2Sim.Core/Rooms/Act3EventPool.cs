using Sts2Sim.Core.Models.Events;

namespace Sts2Sim.Core.Rooms;

/// <summary>Canonical Act 3 (Glory) normal-event pool in game source order.</summary>
public static class Act3EventPool
{
    public static IReadOnlyList<Type> All { get; } = Array.AsReadOnly(new[]
    {
        typeof(BattlewornDummy),
        typeof(GraveOfTheForgotten),
        typeof(HungryForMushrooms),
        typeof(Reflections),
        typeof(RoundTeaParty),
        typeof(Trial),
        typeof(TinkerTime),
    });
}
