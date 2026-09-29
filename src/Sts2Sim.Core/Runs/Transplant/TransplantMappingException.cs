namespace Sts2Sim.Core.Runs.Transplant;

public sealed class TransplantMappingException(string fieldPath, string reason)
    : Exception($"Transplant mapping failed at '{fieldPath}': {reason}")
{
    public string FieldPath { get; } = fieldPath;
}
