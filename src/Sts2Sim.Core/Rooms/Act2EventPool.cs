using Sts2Sim.Core.Models.Events;

namespace Sts2Sim.Core.Rooms;

/// <summary>Canonical Act 2 (Hive) normal-event pool in game source order.</summary>
public static class Act2EventPool
{
    public static IReadOnlyList<Type> All { get; } = Array.AsReadOnly(new[]
    {
        typeof(Amalgamator),
        typeof(Bugslayer),
        typeof(ColorfulPhilosophers),
        typeof(ColossalFlower),
        typeof(FieldOfManSizedHoles),
        typeof(InfestedAutomaton),
        typeof(LostWisp),
        typeof(SpiritGrafter),
        typeof(TheLanternKey),
        typeof(ZenWeaver),
    });
}
