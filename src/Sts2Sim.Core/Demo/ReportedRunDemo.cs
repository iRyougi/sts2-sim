using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Demo;

/// <summary>Runs the deterministic map demo while recording and publishing a complete report.</summary>
public static class ReportedRunDemo
{
    public sealed record Result(
        RunManifest Manifest,
        IReadOnlyDictionary<string, CombatLog> CombatLogs,
        string Markdown,
        string OutputDirectory);

    public static async Task<Result> RunAsync(
        string seed,
        string outputRoot,
        Action<string> log,
        int ascensionLevel = 0,
        string character = "Regent",
        bool fullRun = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(character);
        ArgumentOutOfRangeException.ThrowIfNegative(ascensionLevel);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ascensionLevel, AscensionManager.maxAscensionAllowed);

        RunReplayDemo.EnsureModelsRegistered();
        string fullOutputRoot = Path.GetFullPath(outputRoot);

        CharacterModel characterModel = character.ToUpperInvariant() switch
        {
            "REGENT" => ModelDb.Character<Regent>(),
            "SILENT" => ModelDb.Character<Silent>(),
            _ => throw new ArgumentException("Supported report characters are Regent and Silent.", nameof(character)),
        };
        IReadOnlyList<ActDefinition> acts = fullRun
            ? ActDefinition.GetRandomList(seed)
            : [new Overgrowth()];
        var runState = new RunState(seed, acts, ascensionLevel);
        Player player = Player.CreateForNewRun(characterModel, runState);
        runState.AddPlayer(player);
        var recorder = new RunRecorder();
        var policyRng = new Sts2Sim.Core.Random.Rng(
            StringHelper.GetDeterministicHashCode("policy:" + seed));
        var engine = new RunEngine(
            runState,
            points => policyRng.NextItem(points)!,
            createAncientEventRoom: null,
            recorder: recorder,
            useAvailablePotions: true);
        engine.OnRoomResolved += (point, roomType) =>
            log($"[floor {runState.VisitedMapCoords.Count}] ({point.coord.col},{point.coord.row}) {point.PointType} -> {roomType}");

        log($"Running reported demo (seed=\"{seed}\")...");
        log($"Act sequence: {string.Join(", ", runState.Acts.Select(act => act.GetType().Name))}.");
        RunEngine.Result runResult = await engine.RunAsync(RunReplayDemo.MaxFloors);
        RunManifest manifest = recorder.BuildManifest();
        IReadOnlyDictionary<string, CombatLog> combatLogs = recorder.CombatLogs;
        log($"Run finished: result={(runResult.Won ? "victory" : "defeat")}, floors={manifest.FloorsVisited}, combats={combatLogs.Count}.");

        log("Rendering Markdown in memory...");
        string markdown = MarkdownReportRenderer.Render(manifest, combatLogs);
        string finalDirectory = Path.Combine(fullOutputRoot, manifest.RunId);
        log($"Publishing report to {finalDirectory}...");
        await ReportWriter.WriteWithMarkdownAsync(finalDirectory, manifest, combatLogs, markdown);
        log($"Published {manifest.CombatLogs.Count + 2} files.");

        return new Result(manifest, combatLogs, markdown, finalDirectory);
    }
}
