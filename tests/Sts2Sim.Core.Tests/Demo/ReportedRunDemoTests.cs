using System.Text.Json;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Demo;

public sealed class ReportedRunDemoTests : IDisposable
{
    private readonly string _outputRoot = Path.Combine(
        Path.GetTempPath(),
        "sts2sim-reported-run-demo-tests",
        Guid.NewGuid().ToString("N"));

    public ReportedRunDemoTests()
    {
        ModelDb.ResetForTests();
        Directory.CreateDirectory(_outputRoot);
    }

    [Fact]
    public async Task RunAsync_RecordsAndPublishesOneCompleteJsonAndMarkdownReport()
    {
        var progress = new List<string>();

        ReportedRunDemo.Result result = await ReportedRunDemo.RunAsync(
            "task10-reported-demo",
            _outputRoot,
            progress.Add);

        Assert.NotEmpty(result.Manifest.Floors);
        Assert.NotEmpty(result.CombatLogs);
        Assert.Equal(result.CombatLogs.Count, result.Manifest.CombatLogs.Count);
        Assert.All(result.CombatLogs.Values, combat => Assert.NotEmpty(combat.Turns));
        Assert.Equal(
            Path.Combine(Path.GetFullPath(_outputRoot), result.Manifest.RunId),
            result.OutputDirectory);
        Assert.Contains(progress, line => line.Contains("Publishing", StringComparison.Ordinal));

        string manifestPath = Path.Combine(result.OutputDirectory, "run_manifest.json");
        string markdownPath = Path.Combine(result.OutputDirectory, "report.md");
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(markdownPath));
        using JsonDocument manifestJson = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal(result.Manifest.RunId, manifestJson.RootElement.GetProperty("run_id").GetString());
        Assert.Equal("1.2.0", manifestJson.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal(result.Manifest.CombatLogs.Count, manifestJson.RootElement.GetProperty("combat_logs").GetArrayLength());
        Assert.Equal(result.Markdown, await File.ReadAllTextAsync(markdownPath));
        Assert.Contains("# Run Report:", result.Markdown, StringComparison.Ordinal);

        foreach (var reference in result.Manifest.CombatLogs)
        {
            string combatPath = Path.Combine(
                result.OutputDirectory,
                reference.CombatLogFile.Replace('/', Path.DirectorySeparatorChar));
            using JsonDocument combatJson = JsonDocument.Parse(await File.ReadAllTextAsync(combatPath));
            Assert.Equal(reference.CombatId, combatJson.RootElement.GetProperty("combat_id").GetString());
        }
    }

    [Fact]
    public async Task RunAsync_SameSeedRejectsExistingRunDirectoryWithoutChangingPublishedReport()
    {
        ReportedRunDemo.Result first = await ReportedRunDemo.RunAsync(
            "task10-conflict",
            _outputRoot,
            _ => { });
        string manifestPath = Path.Combine(first.OutputDirectory, "run_manifest.json");
        string originalManifest = await File.ReadAllTextAsync(manifestPath);
        string markerPath = Path.Combine(first.OutputDirectory, "keep.txt");
        await File.WriteAllTextAsync(markerPath, "unchanged");

        await Assert.ThrowsAsync<IOException>(() => ReportedRunDemo.RunAsync(
            "task10-conflict",
            _outputRoot,
            _ => { }));

        Assert.Equal(originalManifest, await File.ReadAllTextAsync(manifestPath));
        Assert.Equal("unchanged", await File.ReadAllTextAsync(markerPath));
    }

    public void Dispose()
    {
        ModelDb.ResetForTests();
        if (Directory.Exists(_outputRoot))
        {
            Directory.Delete(_outputRoot, recursive: true);
        }
    }
}
