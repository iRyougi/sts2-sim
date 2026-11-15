using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class UnknownShopBlacklistReferenceTests : IDisposable
{
    public UnknownShopBlacklistReferenceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact]
    public async Task UnknownRooms_ExcludeShopsAfterShopEntryOrBeforeOnlyShopChildren()
    {
        // JGEN run_history F3 is DrowningBeacon; F5 is Shop; F6 Unknown is RoomFullOfCheese.
        // Preserve the observed Unknown inputs explicitly: v0.113.1's generated map has changed.
        // This tests the resolver/entry bookkeeping, not same-version archive map reconstruction.
        var archive = new RunState("JGENKZEW4VNW", ascensionLevel: 10);
        archive.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), archive));
        archive.AddVisitedMapCoord(new MapCoord(3, 0));
        archive.AddVisitedMapCoord(new MapCoord(6, 1));
        archive.AddVisitedMapCoord(new MapCoord(6, 2));
        var firstUnknown = new MapPoint(6, 2) { PointType = MapPointType.Unknown };
        firstUnknown.AddChildPoint(new MapPoint(6, 3) { PointType = MapPointType.Monster });
        Assert.Equal(RoomType.Event, RoomFactory.ResolveRoomType(archive, firstUnknown));
        archive.AddVisitedMapCoord(new MapCoord(6, 3));
        archive.AddVisitedMapCoord(new MapCoord(6, 4));
        await EnterAndLeave(archive, new MerchantRoom());
        archive.AddVisitedMapCoord(new MapCoord(5, 5));
        var afterShop = new MapPoint(5, 5) { PointType = MapPointType.Unknown };
        afterShop.AddChildPoint(new MapPoint(5, 6) { PointType = MapPointType.Monster });
        var actual = new List<RoomType> { RoomFactory.ResolveRoomType(archive, afterShop) };

        // A Shop reached through Unknown, including a nested shop, still counts as a shop visit.
        var nested = new RunState("unknown-nested-shop");
        nested.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), nested));
        var unknown = new MapPoint(0, 0) { PointType = MapPointType.Unknown };
        nested.AddVisitedMapCoord(unknown.coord);
        SetShopOnlyOdds(nested);
        Assert.Equal(RoomType.Shop, RoomFactory.ResolveRoomType(nested, unknown));
        var outer = new RestSiteRoom();
        nested.PushRoom(outer);
        await outer.Enter(nested);
        await EnterAndLeave(nested, new MerchantRoom());
        await outer.Exit(nested);
        nested.PopCurrentRoom();
        var next = new MapPoint(0, 1) { PointType = MapPointType.Unknown };
        nested.AddVisitedMapCoord(next.coord);
        Assert.False(nested.AddVisitedMapCoord(next.coord)); // A duplicate cannot clear the previous-shop flag.
        SetShopOnlyOdds(nested);
        actual.Add(RoomFactory.ResolveRoomType(nested, next));
        nested.AddVisitedMapCoord(new MapCoord(0, 2));
        SetShopOnlyOdds(nested);
        actual.Add(RoomFactory.ResolveRoomType(nested, new MapPoint(0, 2) { PointType = MapPointType.Unknown }));

        var children = new RunState("unknown-shop-children");
        children.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), children));
        var beforeShops = new MapPoint(0, 0) { PointType = MapPointType.Unknown };
        beforeShops.AddChildPoint(new MapPoint(0, 1) { PointType = MapPointType.Shop });
        beforeShops.AddChildPoint(new MapPoint(1, 1) { PointType = MapPointType.Shop });
        children.AddVisitedMapCoord(beforeShops.coord);
        SetShopOnlyOdds(children);
        actual.Add(RoomFactory.ResolveRoomType(children, beforeShops));
        beforeShops.AddChildPoint(new MapPoint(2, 1) { PointType = MapPointType.Monster });
        SetShopOnlyOdds(children);
        actual.Add(RoomFactory.ResolveRoomType(children, beforeShops));

        // An act transition clears both the current and previous map-point shop-entry state.
        await EnterAndLeave(children, new MerchantRoom());
        children.AddVisitedMapCoord(new MapCoord(0, 1));
        await EnterAndLeave(children, new MerchantRoom());
        children.AdvanceToNextAct();
        children.AddVisitedMapCoord(new MapCoord(0, 0));
        SetShopOnlyOdds(children);
        actual.Add(RoomFactory.ResolveRoomType(children, new MapPoint(0, 0) { PointType = MapPointType.Unknown }));
        Assert.Equal(new[] { RoomType.Event, RoomType.Event, RoomType.Shop,
            RoomType.Event, RoomType.Shop, RoomType.Shop }, actual);
    }

    private static async Task EnterAndLeave(RunState run, AbstractRoom room)
    {
        run.PushRoom(room);
        await room.Enter(run);
        await room.Exit(run);
        run.PopCurrentRoom();
    }

    private static void SetShopOnlyOdds(RunState run)
    {
        run.Odds.UnknownMapPoint.MonsterOdds = 0;
        run.Odds.UnknownMapPoint.TreasureOdds = 0;
        run.Odds.UnknownMapPoint.EliteOdds = -1;
        run.Odds.UnknownMapPoint.ShopOdds = 1;
    }

    public void Dispose() => ModelDb.ResetForTests();
}
