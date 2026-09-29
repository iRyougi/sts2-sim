using System.Globalization;
using System.Text;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Runs;
using Xunit.Abstractions;

namespace Sts2Sim.Core.Tests.Runs;

/// <summary>
/// 一次性测量实验（非回归测试）：中性启发式在 A0 与 A10 上的结局分布。
/// <para>
/// 动机：spec §3.2 记录过 Plan 2-01c 的教训——在 0% 胜率基线上做 A/B，差值恒为 0。
/// A10 显著更难，若中性基线连第一幕都过不去，则 08b-5 的适应度**必须**用
/// <c>ActsCleared</c> / <c>FinalPlayerHp</c> 塑形，不能只用通关率；后续的
/// "全 A vs 混合 A/C"档位对照也要等适应度形状定下来再跑。
/// </para>
/// <para>opt-in：设环境变量 <c>STS2_A10_BASELINE=1</c> 才运行。</para>
/// </summary>
[Collection("ModelDb")]
public sealed class AscensionTenBaselineTests : IDisposable
{
    private const int RunsPerTier = 200;
    private readonly ITestOutputHelper _output;

    public AscensionTenBaselineTests(ITestOutputHelper output)
    {
        _output = output;
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [AscensionTenBaselineFact]
    public async Task MeasureNeutralBaselineAtA0AndA10()
    {
        var report = new StringBuilder();
        report.AppendLine("| 档位 | 局数 | 通关 | 到过Boss | ActsCleared 0/1/2 | 平均终局HP |");
        report.AppendLine("|---:|---:|---:|---:|---|---:|");

        foreach (int ascensionLevel in new[] { 0, 10 })
        {
            var manifests = new List<RunManifest>();
            for (int i = 0; i < RunsPerTier; i++)
            {
                manifests.Add(await RunOnceAsync($"a10-baseline-{i}", ascensionLevel));
            }

            int won = manifests.Count(m => m.Result == "victory");
            int reachedBoss = manifests.Count(m => m.ReachedBoss);
            int[] byActs = [0, 1, 2];
            string spread = string.Join(
                "/",
                byActs.Select(a => manifests.Count(m => m.ActsCleared == a).ToString(CultureInfo.InvariantCulture)));
            double avgHp = manifests.Average(m => m.FinalHp);

            report.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| A{ascensionLevel} | {RunsPerTier} | {won} | {reachedBoss} | {spread} | {avgHp:F1} |"));
        }

        // 实验产物直接打到测试输出，抄进 findings 文档。
        _output.WriteLine(report.ToString());
        Assert.True(report.Length > 0, report.ToString());
    }

    private static async Task<RunManifest> RunOnceAsync(string seed, int ascensionLevel)
    {
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()], ascensionLevel);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        var recorder = new RunRecorder();
        var engine = new RunEngine(
            run,
            points => points.OrderBy(p => p.coord.col).First(),
            createAncientEventRoom: null,
            recorder: recorder,
            useAvailablePotions: false,
            createEventRoom: null);
        var result = await engine.RunAsync(maxFloors: 200);
        var manifest = recorder.BuildManifest();
        // Terminal defeat reporting can lose progress made in earlier acts.
        return manifest with
        {
            ActsCleared = result.Won ? run.Acts.Count : run.CurrentActIndex,
            ReachedBoss = manifest.Floors.Any(floor => floor.PointType == "Boss"),
        };
    }
}
