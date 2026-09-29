namespace Sts2Sim.Core.Map;

/// <summary>逐字移植（<c>MegaCrit.Sts2.Core.Map.MapPointType</c>）。与 <c>Rooms.RoomType</c>（Plan 01）
/// 是两个不同的枚举——"?"（Unknown）点在进入时才通过 <c>UnknownMapPointOdds.Roll</c>（Plan 01）解析成一个
/// 具体 <c>RoomType</c>；<c>Ancient</c> 没有对应的 RoomType（本计划跳过 Ancient 内容，见偏离 #48）。</summary>
public enum MapPointType
{
    Unassigned,
    Unknown,
    Shop,
    Treasure,
    RestSite,
    Monster,
    Elite,
    Boss,
    Ancient,
}
