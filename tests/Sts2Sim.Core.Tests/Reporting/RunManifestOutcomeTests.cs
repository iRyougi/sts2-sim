using System.Text.Json;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Reporting;

/// <summary>
/// manifest 的 <c>result</c> 只能表达"是否通关"，把"死了"和"被 maxFloors 截断"
/// 压成同一个 <c>defeat</c>。08b-5 的 CMA-ES 要用这份报告统计适应度，
/// 必须能把这两者分开，否则会把"跑满上限还活着"误记成"死了"。
/// </summary>
[Collection("ModelDb")]
public sealed class RunManifestOutcomeTests : IDisposable
{
    public RunManifestOutcomeTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void EndRun_Truncated_RecordsSurvivedAndTruncatedAlongsideDefeatResult()
    {
        RunRecorder recorder = BeginOneFloorRun("manifest-outcome-truncated");
        recorder.EndRun(
            won: false,
            floorsVisited: 1,
            finalHp: 44,
            reachedBoss: false,
            survived: true,
            truncated: true,
            actsCleared: 0);

        RunManifest manifest = recorder.BuildManifest();

        // result 保持既有语义（是否通关），不做破坏性变更。
        Assert.Equal("defeat", manifest.Result);
        Assert.True(manifest.Survived);
        Assert.True(manifest.Truncated);
        Assert.False(manifest.ReachedBoss);
        Assert.Equal(0, manifest.ActsCleared);
    }

    [Fact]
    public void EndRun_Death_IsDistinguishableFromTruncation()
    {
        RunRecorder recorder = BeginOneFloorRun("manifest-outcome-death");
        recorder.EndRun(
            won: false,
            floorsVisited: 1,
            finalHp: 0,
            reachedBoss: true,
            survived: false,
            truncated: false,
            actsCleared: 1);

        RunManifest manifest = recorder.BuildManifest();

        Assert.Equal("defeat", manifest.Result);
        Assert.False(manifest.Survived);
        Assert.False(manifest.Truncated);
        Assert.True(manifest.ReachedBoss);
        Assert.Equal(1, manifest.ActsCleared);
    }

    /// <summary>老调用点不传新参数时，<c>survived</c> 必须从终局 HP 推导，而不是取一个撒谎的默认值。</summary>
    [Theory]
    [InlineData(44, true)]
    [InlineData(0, false)]
    public void EndRun_WithoutExplicitSurvived_DerivesItFromFinalHp(int finalHp, bool expectedSurvived)
    {
        RunRecorder recorder = BeginOneFloorRun($"manifest-outcome-derive-{finalHp}");
        recorder.EndRun(won: false, floorsVisited: 1, finalHp: finalHp);

        Assert.Equal(expectedSurvived, recorder.BuildManifest().Survived);
    }

    [Fact]
    public void SerializedManifest_ExposesOutcomeFieldsInSnakeCase()
    {
        RunRecorder recorder = BeginOneFloorRun("manifest-outcome-json");
        recorder.EndRun(
            won: false,
            floorsVisited: 1,
            finalHp: 44,
            reachedBoss: true,
            survived: true,
            truncated: true,
            actsCleared: 1);

        using JsonDocument document = JsonDocument.Parse(
            JsonSerializer.Serialize(
                recorder.BuildManifest(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        JsonElement root = document.RootElement;

        Assert.True(root.GetProperty("reached_boss").GetBoolean());
        Assert.True(root.GetProperty("survived").GetBoolean());
        Assert.True(root.GetProperty("truncated").GetBoolean());
        Assert.Equal(1, root.GetProperty("acts_cleared").GetInt32());
    }

    private static RunRecorder BeginOneFloorRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);

        var recorder = new RunRecorder();
        recorder.BeginRun(runState);
        recorder.EnterFloor(new MapPoint(1, 1) { PointType = MapPointType.Shop }, RoomType.Shop);
        recorder.RecordFloorDetail(new ShopFloorDetail([]));
        recorder.ExitFloor();
        return recorder;
    }
}
