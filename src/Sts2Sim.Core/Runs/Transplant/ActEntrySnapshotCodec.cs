using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Sts2Sim.Core.Runs.Transplant;

/// <summary>Narrow, same-build offline codec. Consumers must CloneReseeded before training continuation.</summary>
public static class ActEntrySnapshotCodec
{
    private static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { info =>
            {
                if (info.Kind == JsonTypeInfoKind.Object)
                    foreach (JsonPropertyInfo property in info.Properties)
                        property.IsRequired = true;
            } }
        }
    };

    internal static string CoreHash => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(RunState).Assembly.Location)));

    public static ActEntrySnapshot ExportActEntry(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return run.ExportActEntryState();
    }

    public static string ExportActEntryJson(RunState run) => JsonSerializer.Serialize(ExportActEntry(run), Json);

    public static RunState ImportActEntry(string json)
    {
        ActEntrySnapshot? snapshot = JsonSerializer.Deserialize<ActEntrySnapshot>(json, Json);
        if (snapshot is null || snapshot.SchemaVersion != ActEntrySnapshot.CurrentSchemaVersion ||
            snapshot.CoreHash != CoreHash)
            throw new TransplantMappingException("ActEntrySnapshot", "Schema or Core build mismatch.");
        return RunState.ImportActEntryState(snapshot);
    }
}
