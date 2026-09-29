using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

/// <summary>
/// A10 DoubleBoss 的运行层：最终幕要打两场 Boss，第二场取 <c>SecondBoss</c>。
/// 逐字对照 <c>RoomSet.NextBossEncounter</c>：
/// <c>bossEncountersVisited != 0 &amp;&amp; SecondBoss != null ? SecondBoss : Boss</c>。
/// </summary>
[Collection("ModelDb")]
public sealed class DoubleBossRunTests : IDisposable
{
    public DoubleBossRunTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact]
    public void ArchivedUnderdocksRuns_FirstBossIsWaterfallGiant()
    {
        // Independent F17 run_history observations; do not derive expected boss from the pool.
        foreach (string seed in new[] { "MVBCYMS8BUST", "R8P62QK7DEDJ", "SKEFLM63695H" })
        {
            var run = new RunState(seed, ascensionLevel: 10);
            run.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), run));
            Assert.Equal("WaterfallGiantBoss", run.PullNextEncounter(RoomType.Boss).Name);
        }
    }
    public void Dispose() => ModelDb.ResetForTests();

    /// <summary>DoubleBoss 只作用于最终幕（<c>RunManager.cs:768</c>：<c>i == Acts.Count - 1</c>）。</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void SecondBossNode_ExistsOnlyOnTheFinalAct(int actIndex, bool expected)
    {
        RunState run = BuildRun("double-boss-final-act", ascensionLevel: 10, actIndex);

        Assert.Equal(expected, run.Map.SecondBossMapPoint is not null);
    }

    [Fact]
    public void AtAscensionZero_NoActHasASecondBoss()
    {
        for (int actIndex = 0; actIndex <= 2; actIndex++)
        {
            RunState run = BuildRun("double-boss-a0", ascensionLevel: 0, actIndex);
            Assert.Null(run.Map.SecondBossMapPoint);
        }
    }

    /// <summary>第一次取 Boss 遭遇拿第一个，第二次拿第二个，两者必须不同。</summary>
    [Fact]
    public void PullNextEncounter_ReturnsTheSecondBossOnTheSecondVisit()
    {
        RunState run = BuildRun("double-boss-encounter", ascensionLevel: 10, actIndex: 2);

        EncounterDefinition first = run.PullNextEncounter(RoomType.Boss);
        EncounterDefinition second = run.PullNextEncounter(RoomType.Boss);
        EncounterDefinition third = run.PullNextEncounter(RoomType.Boss);

        Assert.NotEqual(first, second);
        // 越过两场之后不再变化，避免无限增生。
        Assert.Equal(second, third);
    }

    /// <summary>A0 下反复取 Boss 遭遇恒定返回同一个，语义不能被本改动破坏。</summary>
    [Fact]
    public void PullNextEncounter_AtAscensionZero_AlwaysReturnsTheSameBoss()
    {
        RunState run = BuildRun("double-boss-a0-encounter", ascensionLevel: 0, actIndex: 2);

        Assert.Equal(run.PullNextEncounter(RoomType.Boss), run.PullNextEncounter(RoomType.Boss));
    }

    /// <summary>两套驱动都必须真的走完两场 Boss 才结算通关。</summary>
    [Fact]
    public async Task RunEngine_AtAscensionTen_ResolvesTwoBossRoomsBeforeWinning()
    {
        var resolved = new List<MapPointType>();
        RunState run = BuildRunWithPlayer("glory-integration-0", ascensionLevel: 10);
        var engine = new RunEngine(run, points => points.OrderBy(p => p.coord.col).First());
        engine.OnRoomResolved += (_, roomType) =>
        {
            if (roomType == RoomType.Boss)
            {
                resolved.Add(MapPointType.Boss);
            }
        };

        RunEngine.Result result = await engine.RunAsync(maxFloors: 200);

        var driverResolved = new List<MapPointType>();
        RunState driverRun = BuildRunWithPlayer("glory-integration-0", ascensionLevel: 10);
        var driver = new RunDriver(driverRun, new FirstChoiceDecisionSource());
        driver.OnRoomResolved += (_, roomType) =>
        {
            if (roomType == RoomType.Boss)
            {
                driverResolved.Add(MapPointType.Boss);
            }
        };
        RunDriver.Result driverResult = await driver.RunAsync(maxFloors: 200);

        // 三幕共 4 场 Boss：前两幕各 1 场，最终幕 2 场。
        Assert.True(resolved.Count == 4 && driverResolved.Count == 4,
            $"Expected 4 Boss rooms per driver; engine={resolved.Count}, result={result}, " +
            $"act={run.CurrentActIndex}, point={run.CurrentMapPoint?.PointType}; " +
            $"driver={driverResolved.Count}, result={driverResult}, act={driverRun.CurrentActIndex}, " +
            $"point={driverRun.CurrentMapPoint?.PointType}.");
        Assert.True(result.Won);
        Assert.Equal(3, result.ActsCleared);
        Assert.True(driverResult.Won);
        Assert.Equal(3, driverResult.ActsCleared);
    }

    /// <summary>A10 全链路：四档词缀同时生效，run 能从头跑到尾。</summary>
    [Fact]
    public async Task AscensionTen_FullRun_AppliesEveryModifier()
    {
        RunState run = BuildRunWithPlayer("glory-integration-0", ascensionLevel: 10);
        Player player = run.Players[0];

        // A4 TightBelt：药水槽 -1；A5 AscendersBane：起手牌组带一张诅咒。
        Assert.Contains(player.Deck.Cards, c => c is Sts2Sim.Core.Models.Cards.AscendersBane);
        // A1 SwarmingElites：精英目标数 8。
        Assert.Equal(8, new MapPointTypeCounts(12, 7, run.Ascension).NumOfElites);
        // A6 Inflation：移牌费用基准 100。
        Assert.Equal(100, Sts2Sim.Core.Entities.Merchant.MerchantCardRemovalEntry.PriceFor(run.Ascension, 0));

        var engine = new RunEngine(run, points => points.OrderBy(p => p.coord.col).First());
        RunEngine.Result result = await engine.RunAsync(maxFloors: 200);

        // A10 DoubleBoss：最终幕的地图上确实挂了第二 Boss 节点。
        Assert.NotNull(run.Map.SecondBossMapPoint);
        Assert.True(result.Won);
        Assert.Equal(3, result.ActsCleared);
    }

    private static RunState BuildRun(string seed, int ascensionLevel, int actIndex)
    {
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()], ascensionLevel);
        for (int i = 0; i < actIndex; i++)
        {
            run.AdvanceToNextAct();
        }
        return run;
    }

    private static RunState BuildRunWithPlayer(string seed, int ascensionLevel)
    {
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()], ascensionLevel);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        // Reuse the durable player fixture from ThreeActRunTests to reach the Boss transitions.
        player.Creature.SetMaxHpInternal(999_999_999m);
        player.Creature.HealInternal(999_999_999m);
        return run;
    }
}
