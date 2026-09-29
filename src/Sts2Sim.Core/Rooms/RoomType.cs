namespace Sts2Sim.Core.Rooms;

public enum RoomType
{
    Unassigned,
    Monster,
    Elite,
    Boss,
    Treasure,
    Shop,
    Event,
    RestSite,
    Map,
}

public static class RoomTypeExtensions
{
    public static bool IsCombatRoom(this RoomType roomType) =>
        roomType is RoomType.Monster or RoomType.Elite or RoomType.Boss;
}
