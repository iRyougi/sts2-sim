using Sts2Sim.Core.Models.Events;

namespace Sts2Sim.Core.Rooms;

/// <summary>ModelDb.AllSharedAncients under the existing all-unlocked assumption.</summary>
public static class SharedAncientPool
{
    public static IReadOnlyList<Type> All { get; } = Array.AsReadOnly(new[] { typeof(Darv) });
}
