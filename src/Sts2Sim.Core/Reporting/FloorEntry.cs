namespace Sts2Sim.Core.Reporting;

public sealed record FloorEntry(
    int FloorIndex,
    MapCoordinate Coord,
    string PointType,
    string RoomType,
    int HpBefore,
    int HpAfter,
    int MaxHp,
    int GoldBefore,
    int GoldAfter,
    IReadOnlyList<string> DeckBefore,
    IReadOnlyList<string> DeckAfter,
    IReadOnlyList<string?> RelicsBefore,
    IReadOnlyList<string?> RelicsAfter,
    IReadOnlyList<string?> PotionsBefore,
    IReadOnlyList<string?> PotionsAfter,
    FloorDetail Detail);
