using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.TestSupport;

// Exact-world private state/alias snapshot used by the approved six consumer Theories.
// Snapshots contain strings only, never mutable source-world entities or live delegates.
internal static class RunCloneGraphSnapshot
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FieldInfo[]> Fields = new();

    internal static string Capture(RunState run, object? candidates = null, object? selected = null)
    {
        var text = new StringBuilder();
        var identities = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<object>();
        Append(run);
        Append(candidates);
        Append(selected);
        while (pending.TryDequeue(out object? item))
        {
            int id = identities[item];
            Type type = item.GetType();
            text.Append('\n').Append(id).Append(':').Append(type.FullName).Append('{');
            if (item is Delegate callback)
            {
                foreach (Delegate invocation in Delegate.EnumerateInvocationList(callback))
                {
                    text.Append(invocation.GetType().FullName).Append(':')
                        .Append(invocation.Method.DeclaringType?.FullName).Append(':')
                        .Append(invocation.Method.Name).Append(':').Append(invocation.Method.MetadataToken);
                    Append(invocation.Target);
                }
            }
            else if (item is IEnumerable sequence)
            {
                // Enumerate logical slots in their consumer order, not Dictionary hash buckets
                // or the private layout/capacity of a .NET collection.
                foreach (object? value in sequence) Append(value);
            }
            else
            {
                foreach (FieldInfo field in InstanceFields(type))
                {
                    if (IsRuntimeControl(field)) continue;
                    text.Append(field.DeclaringType?.FullName).Append('.').Append(field.Name).Append('=');
                    Append(field.GetValue(item));
                }
            }
            text.Append('}');
        }
        // Retain every UTF-16 code unit, including unpaired surrogates. This is
        // lossless storage, not a digest or a reduced state projection.
        // Write builder chunks directly so a second full-size string is not allocated.
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            foreach (ReadOnlyMemory<char> chunk in text.GetChunks())
                deflate.Write(MemoryMarshal.AsBytes(chunk.Span));
        return Convert.ToBase64String(compressed.GetBuffer(), 0, checked((int)compressed.Length));

        void Append(object? value)
        {
            if (value is null) { text.Append("null;"); return; }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal or Guid or DateTime or DateTimeOffset or TimeSpan)
            {
                text.Append(type.FullName).Append(':')
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';');
                return;
            }
            if (value is string stringValue)
            {
                text.Append('s').Append(stringValue.Length).Append(':').Append(stringValue).Append(';');
                return;
            }
            if (value is Type metadata)
            {
                text.Append("type:").Append(metadata.FullName).Append(';');
                return;
            }
            if (value is AbstractModel { IsCanonical: true } canonical)
            {
                text.Append("canonical:").Append(type.FullName).Append(':').Append(canonical.Id).Append(';');
                return;
            }
            if (value is MemberInfo member)
            {
                text.Append("member:").Append(member.DeclaringType?.FullName).Append(':')
                    .Append(member.Name).Append(':').Append(member.MetadataToken).Append(';');
                return;
            }
            if (value is Task || value is IAsyncStateMachine)
                throw new InvalidOperationException($"Unclassified runtime handle in exact-world snapshot: {type.FullName}.");
            if (value is StringComparer || ReferenceEquals(value, ReferenceEqualityComparer.Instance)
                || type.Assembly == typeof(EqualityComparer<>).Assembly && type.Namespace == "System.Collections.Generic"
                    && type.GetInterfaces().Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IEqualityComparer<>) || i.GetGenericTypeDefinition() == typeof(IComparer<>))))
            {
                text.Append("comparer:").Append(type.FullName).Append(';');
                return;
            }
            if (type != typeof(object) && type.Assembly != typeof(RunState).Assembly && value is not IEnumerable && value is not Delegate
                && !type.IsValueType && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                throw new InvalidOperationException($"Unclassified mutable graph data: {type.FullName}.");
            if (type.IsValueType)
            {
                text.Append(type.FullName).Append('(');
                foreach (FieldInfo field in InstanceFields(type))
                {
                    text.Append(field.Name).Append('=');
                    Append(field.GetValue(value));
                }
                text.Append(");");
                return;
            }
            if (!identities.TryGetValue(value, out int identity))
            {
                identity = identities.Count;
                identities.Add(value, identity);
                pending.Enqueue(value);
            }
            text.Append('@').Append(identity).Append(';');
        }
    }

    private static bool IsRuntimeControl(FieldInfo field) =>
        field.Name is "_stateLock" or "_lifecycleLock" or "_rewardLock" or "_takeLock" or "_resolutionLock"
            or "_purchaseGate" or "_revealGate" or "_interactionGate" or "_insideLifecycle" or "_observer"
            or "_cardSelectionSource" or "<CardSelectionSource>k__BackingField"
            or "_activeChoiceTask" or "_activeChoiceOption" or "_activeChoicePageVersion"
            or "_takeTask" or "_resolutionTask" or "_outcomeTask" or "_exitTask" or "_completionTask"
        || field.DeclaringType == typeof(CombatState) && field.Name == "_hookListenerSnapshotCache"
        || field.DeclaringType == typeof(AbstractModel) && field.Name == "ExecutionFinished"
        || field.DeclaringType == typeof(RelicModel) && field.Name == "Flashed";

    private static FieldInfo[] InstanceFields(Type type) => Fields.GetOrAdd(type, static root =>
    {
        var fields = new List<FieldInfo>();
        for (Type? current = root; current is not null && current != typeof(object); current = current.BaseType)
            fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        return fields.OrderBy(field => field.DeclaringType?.FullName, StringComparer.Ordinal)
            .ThenBy(field => field.Name, StringComparer.Ordinal).ToArray();
    });
}
