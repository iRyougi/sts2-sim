using System.Text.Json;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Runs.Transplant;

public sealed record MapCoordDto(int Col, int Row);

public sealed record CardSnapshot(
    string Id,
    int UpgradeLevel,
    string? EnchantmentId,
    int? EnchantmentAmount,
    int? FloorAddedToDeck,
    IReadOnlyDictionary<string, JsonElement> Props);

public sealed record RelicSnapshot(string Id, IReadOnlyDictionary<string, JsonElement> Props);

public sealed record RelicBagSnapshot(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Buckets);

public sealed record RoomProgressSnapshot(
    int EventsVisited,
    int NormalEncountersVisited,
    int EliteEncountersVisited,
    int BossEncountersVisited,
    IReadOnlyList<string> VisitedEventIds,
    IReadOnlyList<string> VisitedNormalEncounterIds,
    IReadOnlyList<string> VisitedEliteEncounterIds,
    IReadOnlyList<string> VisitedBossEncounterIds,
    string? NextEncounterId)
{
    public int? NextRoomId { get; init; }
    public bool? CurrentMapPointHasShop { get; init; }
    public bool? PreviousMapPointHasShop { get; init; }
}

public sealed record PlayerSnapshot(
    int CurrentHp,
    int MaxHp,
    int Gold,
    int CardRemovalsUsed,
    IReadOnlyList<CardSnapshot> Deck,
    IReadOnlyList<RelicSnapshot> Relics,
    IReadOnlyList<string?> PotionSlots,
    SerializablePlayerRngSet PlayerRng,
    SerializablePlayerOddsSet PlayerOdds,
    RelicBagSnapshot RelicBag)
{
    public int? MaxEnergy { get; init; }
    public int? BaseOrbSlotCount { get; init; }
}


public sealed record RunTransplantSnapshot(
    int SchemaVersion,
    string Seed,
    string CharacterId,
    int Ascension,
    int ActIndex,
    IReadOnlyList<string> ActIds,
    IReadOnlyList<MapCoordDto> VisitedMapCoords,
    int CompletedActFloors,
    MapCoordDto? TargetNode,
    string TargetRoomType,
    PlayerSnapshot Player,
    SerializableRunRngSet RunRng,
    SerializableRunOddsSet RunOdds,
    RoomProgressSnapshot RoomProgress,
    RelicBagSnapshot SharedRelicBag,
    IReadOnlyDictionary<string, JsonElement> ExtraFields)
{
    public const int CurrentSchemaVersion = 1;
    public bool IsInjected { get; init; }
}
