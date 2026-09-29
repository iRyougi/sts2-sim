using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Rooms;

internal sealed record TreasureRoomResolution(
    Player Player,
    int GoldGained,
    string? RelicGained);
