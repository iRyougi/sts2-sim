namespace Sts2Sim.Core.Rl;

public enum DecisionType
{
    Combat,
    MapPoint,
    Reward,
    Shop,
    RestSite,
    Event,
    CardSelection,
    CustomEvent,
}

public sealed record PlayerSnapshot(
    int CurrentHp,
    int MaxHp,
    int Block,
    int Energy,
    int Gold,
    int PotionSlots)
{
    public PlayerSnapshot(int CurrentHp, int MaxHp, int Block, int Energy, int Gold)
        : this(CurrentHp, MaxHp, Block, Energy, Gold, PotionSlots: 0)
    {
    }
}

public sealed record CardSnapshot(
    string ModelId,
    int EnergyCost,
    bool CanPlay,
    int UpgradeCount,
    bool IsCurse,
    string? EnchantmentId)
{
    public CardSnapshot(string ModelId, int EnergyCost, bool CanPlay)
        : this(ModelId, EnergyCost, CanPlay, UpgradeCount: 0, IsCurse: false, EnchantmentId: null)
    {
    }
}

public sealed record EnemySnapshot(int CurrentHp, int MaxHp, int Block, string IntentType, int IntentDamage);

public sealed record MapNodeSnapshot(
    int Col,
    int Row,
    string PointType,
    bool Visited,
    IReadOnlyList<int> OutgoingRows)
{
    public MapNodeSnapshot(int Col, int Row, string PointType, bool Visited)
        : this(Col, Row, PointType, Visited, Array.Empty<int>())
    {
    }
}

public sealed record MapSnapshot(
    IReadOnlyList<MapNodeSnapshot> Nodes,
    int CurrentCol,
    int CurrentRow,
    int FloorsVisited);

/// <summary>
/// Identifies the candidate represented by a legal positional action slot.
/// </summary>
public sealed record CandidateSlot(int SlotIndex, string Label);

/// <summary>
/// Protobuf-independent observation data grouped by decision context.
/// </summary>
public sealed record ObservationSnapshot(
    DecisionType DecisionType,
    PlayerSnapshot Player,
    IReadOnlyList<CardSnapshot> Hand,
    IReadOnlyList<CardSnapshot> DeckView,
    IReadOnlyList<EnemySnapshot> Enemies,
    MapSnapshot Map,
    IReadOnlyList<RelicSnapshot> Relics,
    IReadOnlyList<PotionSnapshot> Potions,
    int AscensionLevel,
    int ActIndex,
    IReadOnlyList<CandidateSlot> Candidates,
    IReadOnlyList<bool> LegalActionMask,
    int SelectionMinCount = 0,
    int SelectionMaxCount = 0,
    int SelectionSelectedCount = 0,
    bool SelectionCancelable = false)
{
    public ObservationSnapshot(
        DecisionType DecisionType,
        PlayerSnapshot Player,
        IReadOnlyList<CardSnapshot> Hand,
        IReadOnlyList<CardSnapshot> DeckView,
        IReadOnlyList<EnemySnapshot> Enemies,
        MapSnapshot Map,
        IReadOnlyList<CandidateSlot> Candidates,
        IReadOnlyList<bool> LegalActionMask,
        int SelectionMinCount = 0,
        int SelectionMaxCount = 0,
        int SelectionSelectedCount = 0,
        bool SelectionCancelable = false)
        : this(
            DecisionType,
            Player,
            Hand,
            DeckView,
            Enemies,
            Map,
            Array.Empty<RelicSnapshot>(),
            Array.Empty<PotionSnapshot>(),
            AscensionLevel: 0,
            ActIndex: 0,
            Candidates,
            LegalActionMask,
            SelectionMinCount,
            SelectionMaxCount,
            SelectionSelectedCount,
            SelectionCancelable)
    {
    }
}
