using Sts2Sim.Core.Models.Events;

namespace Sts2Sim.Core.Rooms;

/// <summary>Canonical Act 3 (Glory) normal-event pool in v0.113.1 source order.</summary>
public static class Act3EventPool
{
    public static IReadOnlyList<Type> All { get; } = Array.AsReadOnly(new[]
    {
        typeof(BattlewornDummy),
        typeof(GraveOfTheForgotten),
        typeof(HungryForMushrooms),
        typeof(Reflections),
        typeof(RoundTeaParty),
        typeof(ShrineOfTheWarlord),
        typeof(ThermaeNonupeipe),
        typeof(TinkerTime),
        typeof(Trial),
    });
}
