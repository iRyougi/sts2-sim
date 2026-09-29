using Sts2Sim.Core.Entities.Rngs;

namespace Sts2Sim.Core.Saves;

public class SerializablePlayerRngSet
{
    public ulong Seed { get; init; }

    public Dictionary<PlayerRngType, SerializableRng> Rngs { get; init; } = new();
}
