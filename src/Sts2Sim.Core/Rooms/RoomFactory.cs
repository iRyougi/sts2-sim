using Sts2Sim.Core.Content;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rooms;

/// <summary>
/// Resolves map points to room types and creates the corresponding room instances.
/// </summary>
public static class RoomFactory
{
    public static RoomType ResolveRoomType(RunState runState, MapPoint point)
    {
        switch (point.PointType)
        {
            case MapPointType.Monster:
                return RoomType.Monster;
            case MapPointType.Elite:
                return RoomType.Elite;
            case MapPointType.Boss:
                return RoomType.Boss;
            case MapPointType.Shop:
                return RoomType.Shop;
            case MapPointType.Treasure:
                return RoomType.Treasure;
            case MapPointType.RestSite:
                return RoomType.RestSite;
            case MapPointType.Ancient:
                return RoomType.Event;
            case MapPointType.Unknown:
                // RunManager.BuildRoomTypeBlacklist excludes consecutive shops and shops before
                // a node whose nonempty set of outgoing paths all lead directly to shops.
                bool excludeShop = runState.PreviousMapPointHasShop ||
                    (point.Children.Count > 0 && point.Children.All(child => child.PointType == MapPointType.Shop));
                return runState.Odds.UnknownMapPoint.Roll(
                    excludeShop ? new[] { RoomType.Shop } : Array.Empty<RoomType>(),
                    runState.Rng.ForSemanticKey(
                        RunRngType.UnknownMapPoint,
                        $"act={runState.CurrentActIndex}/floor={runState.TotalFloor}/map_col={point.coord.col}/map_row={point.coord.row}/room_kind=unknown"));
            default:
                throw new ArgumentOutOfRangeException(nameof(point), point.PointType, null);
        }
    }

    public static AbstractRoom CreateRoom(RunState runState, RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Monster => new CombatRoom(() => CreateEncounter(runState, RoomType.Monster), RoomType.Monster),
            RoomType.Elite => new CombatRoom(() => CreateEncounter(runState, RoomType.Elite), RoomType.Elite),
            RoomType.Boss => new CombatRoom(() => CreateEncounter(runState, RoomType.Boss), RoomType.Boss),
            RoomType.RestSite => new RestSiteRoom(),
            RoomType.Treasure => new TreasureRoom(),
            RoomType.Shop => new MerchantRoom(),
            RoomType.Event => CreateMapPointEventRoom(runState),
            _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, null),
        };
    }

    private static EventRoom CreateMapPointEventRoom(RunState runState)
    {
        Type eventType = runState.PullMapPointEvent();
        return new EventRoom(() => (EventModel)ModelDb.Get(eventType).MutableClone());
    }

    public static AbstractRoom CreateAncientEventRoom(RunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        Type ancientEventType = runState.CurrentAncientEventType;
        return new EventRoom(() => (EventModel)ModelDb.Get(ancientEventType).MutableClone());
    }

    private static (
        EncounterDefinition Encounter,
        IReadOnlyList<(MonsterModel Monster, string? SlotName)> Monsters) CreateEncounter(
        RunState runState,
        RoomType roomType)
    {
        EncounterDefinition encounter = runState.PullNextEncounterForTransplant(roomType);
        Rng monsterRng = CreateEncounterMonsterRng(runState, encounter);
        return (encounter, encounter.CreateMonsters(monsterRng));
    }

    /// <summary>真实游戏 <c>EncounterModel.GenerateMonstersWithSlots</c> 用一个独立于12个具名
    /// <c>RunRngSet</c> 流的派生种子（由运行种子、当前总楼层数与遭遇战 ID 哈希组成）生成怪物,
    /// 不复用任何共享流——这样同一个遭遇战被打第二次(比如强制战斗)也不会跟别的抽取互相干扰。</summary>
    private static Rng CreateEncounterMonsterRng(RunState runState, EncounterDefinition encounter) =>
        new(EncounterMonsterSeed(runState.Rng.Seed, runState.TotalFloor, encounter));

    /// <summary>楼层项是跨幕累计的 <c>TotalFloor</c>（原版 <c>MapPointHistory</c> 逐幕求和），不是本幕的
    /// <c>VisitedMapCoords.Count</c>——后者进下一幕清零，第二、三幕的怪物组成和首回合招式会全部偏掉。
    /// 哈希项是原版 <c>Id.Entry</c>（<see cref="EncounterDefinition.IdEntry"/>），不是模拟器的遭遇名。</summary>
    internal static ulong EncounterMonsterSeed(ulong runSeed, int totalFloor, EncounterDefinition encounter) =>
        (ulong)((long)runSeed + totalFloor) + StringHelper.GetDeterministicHashCode(encounter.IdEntry);
}
