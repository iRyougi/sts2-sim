using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Reporting;

[Collection("ModelDb")]
public sealed class RunRecorderFixRound1Tests : IDisposable
{
    public RunRecorderFixRound1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RecordEnemyAction_RealFogmogSummonCorrelatesExistingEnemyAndPreservesSummon()
    {
        RunState runState = CreateRun("recorder-fogmog-summon");
        Player player = runState.Players.Single();
        var fogmog = (Fogmog)ModelDb.Monster<Fogmog>().MutableClone();
        var room = new CombatRoom(() => new[] { ((MonsterModel)fogmog, (string?)"fogmog") });
        runState.PushRoom(room);
        await room.Enter(runState);
        var recorder = new RunRecorder();
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        string combatId = recorder.BeginCombat(
            runState,
            RoomType.Monster,
            "Fogmog",
            room.Engine.State);
        recorder.RecordTurnStart(room.Engine.State);
        PlayerSnapshot playerBefore = SnapshotFactory.SnapshotPlayer(player);
        EnemySnapshot[] enemiesBefore = room.Engine.State.Enemies
            .Select(SnapshotFactory.SnapshotEnemy)
            .ToArray();

        await room.Engine.EndPlayerTurnAsync();

        PlayerSnapshot playerAfter = SnapshotFactory.SnapshotPlayer(player);
        EnemySnapshot[] enemiesAfter = room.Engine.State.Enemies
            .Select(SnapshotFactory.SnapshotEnemy)
            .ToArray();
        recorder.RecordEnemyAction(
            fogmog.Creature,
            "ILLUSION_MOVE",
            [new ActionSnapshotSegment(
                playerBefore,
                playerAfter,
                enemiesBefore,
                enemiesAfter)]);
        recorder.RecordEndTurn(playerAfter, enemiesAfter);
        recorder.EndCombat(
            victory: false,
            playerAfter,
            enemiesAfter,
            new CombatRewards(0, [], null, null, null));
        recorder.ExitFloor();
        recorder.EndRun(won: false, floorsVisited: 1, finalHp: player.Creature.CurrentHp);

        TurnRecord turn = Assert.Single(recorder.BuildCombatLog(combatId).Turns);
        Assert.Equal(["fogmog", "illusion"], turn.EnemiesPost.Select(enemy => enemy.Slot));
        Assert.Equal("fogmog", Assert.IsType<EnemyAction>(turn.Actions[0]).Source);
    }

    [Fact]
    public void RecordFloorDetail_CopiesShopPurchasesBeforeCallerMutation()
    {
        RunState runState = CreateRun("recorder-shop-copy");
        var recorder = new RunRecorder();
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(1, 1) { PointType = MapPointType.Shop },
            RoomType.Shop);
        var purchases = new List<PurchaseRecord>
        {
            new("Card", "CARD.STRIKE_REGENT", 50),
        };
        recorder.RecordFloorDetail(new ShopFloorDetail(purchases));

        purchases.Add(new PurchaseRecord("Relic", "RELIC.DIVINE_RIGHT", 100));
        recorder.ExitFloor();
        recorder.EndRun(won: false, floorsVisited: 1, finalHp: 75);

        var detail = Assert.IsType<ShopFloorDetail>(Assert.Single(recorder.BuildManifest().Floors).Detail);
        PurchaseRecord purchase = Assert.Single(detail.Purchases);
        Assert.Equal("CARD.STRIKE_REGENT", purchase.Id);
    }

    [Fact]
    public void RecordFloorDetail_RejectsCallerProvidedDetailForCombatFloor()
    {
        RunState runState = CreateRun("recorder-combat-detail-owner");
        var recorder = new RunRecorder();
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => recorder.RecordFloorDetail(new ShopFloorDetail([])));

        Assert.Contains("combat", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordFloorDetail_ValidatesNonCombatTypeAndAllowsAncientEventMapping()
    {
        RunState mismatchedRun = CreateRun("recorder-floor-mismatch");
        var mismatched = new RunRecorder();
        mismatched.BeginRun(mismatchedRun);
        mismatched.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.RestSite },
            RoomType.RestSite);
        InvalidOperationException mismatchError = Assert.Throws<InvalidOperationException>(
            () => mismatched.RecordFloorDetail(new ShopFloorDetail([])));
        Assert.Contains("RestSite", mismatchError.Message, StringComparison.OrdinalIgnoreCase);

        RunState ancientRun = CreateRun("recorder-ancient-event");
        var ancient = new RunRecorder();
        ancient.BeginRun(ancientRun);
        ancient.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Ancient },
            RoomType.Event);
        ancient.RecordFloorDetail(new AncientFloorDetail("Neow", "GoldenPearl"));
        ancient.ExitFloor();
        ancient.EndRun(won: false, floorsVisited: 1, finalHp: 75);

        Assert.IsType<AncientFloorDetail>(Assert.Single(ancient.BuildManifest().Floors).Detail);
    }

    private static RunState CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }
}
