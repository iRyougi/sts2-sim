using Sts2Sim.Core.Entities.Rngs;

namespace Sts2Sim.Core.Saves;

public class SerializableRunRngSet
{
    public string Seed { get; init; } = string.Empty;

    public Dictionary<RunRngType, SerializableRng> Rngs { get; init; } = new();
}
