namespace Sts2Sim.Core.Reporting;

public sealed record TreasureFloorDetail(
    int GoldGained,
    string? RelicGained,
    // Omitted when null so Standard treasure JSON keeps its previous shape (#1034).
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? ExtraRelicsGained = null) : FloorDetail;
