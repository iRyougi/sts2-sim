using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Runs.Transplant;

public sealed record ActEntryRooms(IReadOnlyList<string> Events, IReadOnlyList<string> Normal,
    IReadOnlyList<string> Elite, string Boss, string? SecondBoss, string Ancient);
public sealed record ActEntryModelRef(string Kind, int Index);
public sealed record ActEntryMapPoint(int Col, int Row, string Type, bool CanBeModified, bool InGrid,
    IReadOnlyList<MapCoordDto> Children, IReadOnlyList<MapCoordDto> Parents, IReadOnlyList<ActEntryModelRef> Quests);
public sealed record ActEntryMap(string Kind, int Columns, int Rows, MapCoordDto Starting,
    MapCoordDto Boss, MapCoordDto? SecondBoss, IReadOnlyList<MapCoordDto> Starts, IReadOnlyList<ActEntryMapPoint> Points);

/// <summary>Offline private state, admissible only at act 2 before Ancient entry. Never a model observation.</summary>
public sealed record ActEntrySnapshot(int SchemaVersion, string CoreHash, string Seed, string CharacterId,
    int Ascension, int ActIndex, IReadOnlyList<string> Acts, RunEntryPhase Phase,
    IReadOnlyList<MapCoordDto> Visited, int CompletedFloors, PlayerSnapshot Player, decimal Block,
    bool CanUsePotions, int BaseOrbSlots, SerializableRunRngSet RunRng, SerializableRunOddsSet RunOdds,
    UnknownMapPointBaseRules BaseRules, UnknownMapPointBaseRules ResetBaseRules,
    RelicBagSnapshot SharedRelicBag, ActEntryMap Map, IReadOnlyList<ActEntryRooms> Rooms,
    IReadOnlyDictionary<int, string> AncientHistory, bool CompleteAncientHistory,
    IReadOnlyList<string> VisitedEvents, int EventsVisited, int NormalVisited, int EliteVisited,
    int BossVisited, int NextRoomId, bool CurrentShop, bool PreviousShop,
    int ProgressWongo, int WongoEarned, bool FreedRepy, IReadOnlyList<string> CompletedQuests,
    bool Imagination)
{
    public const int CurrentSchemaVersion = 1;
}