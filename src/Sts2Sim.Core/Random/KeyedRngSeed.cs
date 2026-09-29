using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Random;

/// <summary>Derives a generator seed from a run seed, stream name, and semantic purpose.</summary>
internal static class KeyedRngSeed
{
    private const string Version = "sts2-crn-key/v1";

    public static ulong Derive(ulong runSeed, string streamName, string semanticKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticKey);

        string canonical =
            $"{Version}|seed={runSeed:x16}|stream={streamName.Length}:{streamName}|" +
            $"key={semanticKey.Length}:{semanticKey}";
        return StringHelper.GetDeterministicHashCode(canonical);
    }
}
