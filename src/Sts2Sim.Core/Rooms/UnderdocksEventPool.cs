using Sts2Sim.Core.Models.Events;

namespace Sts2Sim.Core.Rooms;

/// <summary>Underdocks events; ActDefinition.EffectiveEventPool appends the existing SharedEventPool once.</summary>
public static class UnderdocksEventPool
{
    public static IReadOnlyList<Type> All { get; } = Array.AsReadOnly(new[]
    {
        typeof(AbyssalBaths),
        typeof(DrowningBeacon),
        typeof(EndlessConveyor),
        typeof(PunchOff),
        typeof(SpiralingWhirlpool),
        typeof(SunkenStatue),
        typeof(SunkenTreasury),
        typeof(DoorsOfLightAndDark),
        typeof(TrashHeap),
        typeof(WaterloggedScriptorium),
    });
}
