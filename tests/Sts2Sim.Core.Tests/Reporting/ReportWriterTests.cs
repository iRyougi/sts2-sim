using System.Text;
using System.Text.Json;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Reporting;

public sealed class ReportWriterTests : IDisposable
{
    private readonly string _outputDirectory = Path.Combine(
        Path.GetTempPath(),
        "sts2sim-report-writer-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task WriteAsync_WritesSnakeCaseUtf8ManifestAndPerCombatFiles()
    {
        RunManifest manifest = CreateManifest(result: "victory");
        CombatLog combat = CreateCombatLog(combatId: "combat-001", floor: 1);

        await ReportWriter.WriteAsync(
            _outputDirectory,
            manifest,
            new Dictionary<string, CombatLog> { [combat.CombatId] = combat });

        string manifestPath = Path.Combine(_outputDirectory, "run_manifest.json");
        string combatPath = Path.Combine(_outputDirectory, "combats", "001_combat-001.json");
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(combatPath));
        Assert.False(File.ReadAllBytes(manifestPath).AsSpan().StartsWith(Encoding.UTF8.Preamble));

        using JsonDocument manifestJson = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        JsonElement manifestRoot = manifestJson.RootElement;
        Assert.Equal("1.2.0", manifestRoot.GetProperty("schema_version").GetString());
        Assert.Equal("run-雪", manifestRoot.GetProperty("run_id").GetString());
        Assert.Equal(1, manifestRoot.GetProperty("floors")[0].GetProperty("floor_index").GetInt32());
        Assert.False(manifestRoot.TryGetProperty("SchemaVersion", out _));
        Assert.Equal("combat", manifestRoot.GetProperty("floors")[0].GetProperty("detail").GetProperty("type").GetString());

        using JsonDocument combatJson = JsonDocument.Parse(await File.ReadAllTextAsync(combatPath));
        JsonElement combatRoot = combatJson.RootElement;
        Assert.Equal("combat-001", combatRoot.GetProperty("combat_id").GetString());
        Assert.Equal("use_potion", combatRoot.GetProperty("turns")[0].GetProperty("actions")[0].GetProperty("t").GetString());
        Assert.Equal(7, combatRoot.GetProperty("turns")[0].GetProperty("actions")[0].GetProperty("healing_received").GetInt32());
        Assert.Equal("CARD.STRIKE_REGENT", combatRoot.GetProperty("rewards").GetProperty("cards_taken")[0].GetString());
    }

    [Fact]
    public async Task WriteAsync_DefaultCancellationTokenPreservesLegacySourceCompatibility()
    {
        CombatLog combat = CreateCombatLog(combatId: "combat-001", floor: 1);

        await ReportWriter.WriteAsync(
            _outputDirectory,
            CreateManifestFor(combat),
            Logs(combat),
            default);

        Assert.True(File.Exists(Path.Combine(_outputDirectory, "run_manifest.json")));
    }

    [Fact]
    public async Task WriteWithMarkdownAsync_PublishesReportMdWithJsonAsUtf8WithoutBom()
    {
        RunManifest manifest = CreateManifest(result: "victory");
        CombatLog combat = CreateCombatLog(combatId: "combat-001", floor: 1);
        const string markdown = "# Run snow\n\ncomplete\n";

        await ReportWriter.WriteWithMarkdownAsync(
            _outputDirectory,
            manifest,
            Logs(combat),
            markdown);

        string markdownPath = Path.Combine(_outputDirectory, "report.md");
        Assert.Equal(markdown, await File.ReadAllTextAsync(markdownPath));
        Assert.False(File.ReadAllBytes(markdownPath).AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.True(File.Exists(Path.Combine(_outputDirectory, "run_manifest.json")));
        Assert.True(File.Exists(Path.Combine(_outputDirectory, "combats", "001_combat-001.json")));
        Assert.Empty(ScratchDirectories());
    }

    [Fact]
    public async Task WriteWithMarkdownAsync_WriteFailureLeavesNeitherJsonNorMarkdownPublished()
    {
        CombatLog combat = CreateCombatLog("combat-001", floor: 1);
        ReportWriter.WriteFileAsync failOnMarkdownWrite = InterruptOnWrite(
            writeNumber: 3,
            () => throw new IOException("Injected Markdown write failure."));

        await Assert.ThrowsAsync<IOException>(() => ReportWriter.WriteWithMarkdownAndFileWriterAsync(
            _outputDirectory,
            CreateManifestFor(combat),
            Logs(combat),
            "# Complete report\n",
            failOnMarkdownWrite));

        Assert.False(Directory.Exists(_outputDirectory));
        Assert.Empty(ScratchDirectories());
    }

    [Fact]
    public async Task WriteAsync_RejectsExistingOutputDirectoryWithoutChangingIt()
    {
        CombatLog combat = CreateCombatLog(combatId: "combat-001", floor: 1);
        var combats = new Dictionary<string, CombatLog> { [combat.CombatId] = combat };
        Directory.CreateDirectory(_outputDirectory);
        string markerPath = Path.Combine(_outputDirectory, "keep.txt");
        await File.WriteAllTextAsync(markerPath, "unchanged");

        await Assert.ThrowsAsync<IOException>(() =>
            ReportWriter.WriteAsync(_outputDirectory, CreateManifest(result: "victory"), combats));

        Assert.Equal("unchanged", await File.ReadAllTextAsync(markerPath));
        Assert.Equal(["keep.txt"], Directory.GetFiles(_outputDirectory).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("folder/name")]
    [InlineData("folder\\name")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task WriteAsync_RejectsCombatIdsThatAreNotSafeFileNameSegments(string combatId)
    {
        CombatLog combat = CreateCombatLog(combatId, floor: 1);

        await Assert.ThrowsAsync<ArgumentException>(() => ReportWriter.WriteAsync(
            _outputDirectory,
            CreateManifest(result: "victory"),
            new Dictionary<string, CombatLog> { ["entry"] = combat }));

        Assert.False(Directory.Exists(_outputDirectory));
    }

    [Fact]
    public async Task WriteAsync_RejectsDuplicateCombatDestinationsBeforeWriting()
    {
        CombatLog combat = CreateCombatLog(combatId: "same", floor: 1);
        var combats = new Dictionary<string, CombatLog>
        {
            ["first"] = combat,
            ["second"] = combat,
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportWriter.WriteAsync(_outputDirectory, CreateManifest(result: "victory"), combats));

        Assert.False(Directory.Exists(_outputDirectory));
    }

    [Fact]
    public async Task WriteAsync_ObservesPreCanceledTokenBeforeCreatingOutput()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReportWriter.WriteAsync(
            _outputDirectory,
            CreateManifest(result: "victory"),
            new Dictionary<string, CombatLog>(),
            source.Token));

        Assert.False(Directory.Exists(_outputDirectory));
    }

    [Fact]
    public async Task WriteAsync_WriteFailureDuringStagingLeavesNoPublishedOrScratchDirectory()
    {
        CombatLog combat = CreateCombatLog("combat-001", floor: 1);
        ReportWriter.WriteFileAsync failOnSecondWrite = InterruptOnWrite(
            writeNumber: 2,
            () => throw new IOException("Injected write failure."));

        await Assert.ThrowsAsync<IOException>(() => ReportWriter.WriteWithFileWriterAsync(
            _outputDirectory,
            CreateManifestFor(combat),
            Logs(combat),
            failOnSecondWrite));

        Assert.False(Directory.Exists(_outputDirectory));
        Assert.Empty(ScratchDirectories());
    }

    [Fact]
    public async Task WriteAsync_CancellationDuringStagingLeavesNoPublishedOrScratchDirectory()
    {
        CombatLog combat = CreateCombatLog("combat-001", floor: 1);
        using var source = new CancellationTokenSource();
        ReportWriter.WriteFileAsync cancelOnSecondWrite = InterruptOnWrite(
            writeNumber: 2,
            source.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReportWriter.WriteWithFileWriterAsync(
            _outputDirectory,
            CreateManifestFor(combat),
            Logs(combat),
            cancelOnSecondWrite,
            source.Token));

        Assert.False(Directory.Exists(_outputDirectory));
        Assert.Empty(ScratchDirectories());
    }

    [Theory]
    [InlineData("missing_index")]
    [InlineData("extra_index")]
    [InlineData("dictionary_key")]
    [InlineData("floor")]
    [InlineData("combat_log_file")]
    [InlineData("run_id")]
    [InlineData("manifest_schema")]
    [InlineData("combat_schema")]
    public async Task WriteAsync_RejectsIndexMismatchBeforeCreatingOutput(string mismatch)
    {
        CombatLog combat = CreateCombatLog("combat-001", floor: 1);
        RunManifest manifest = CreateManifestFor(combat);
        IReadOnlyDictionary<string, CombatLog> logs = Logs(combat);

        switch (mismatch)
        {
            case "missing_index":
                manifest = manifest with { CombatLogs = [] };
                break;
            case "extra_index":
                manifest = manifest with
                {
                    CombatLogs =
                    [
                        manifest.CombatLogs[0],
                        new CombatLogRef("extra", 3, "combats/003_extra.json"),
                    ],
                };
                break;
            case "dictionary_key":
                logs = new Dictionary<string, CombatLog> { ["wrong-key"] = combat };
                break;
            case "floor":
                manifest = manifest with
                {
                    CombatLogs = [manifest.CombatLogs[0] with { Floor = 9 }],
                };
                break;
            case "combat_log_file":
                manifest = manifest with
                {
                    CombatLogs = [manifest.CombatLogs[0] with { CombatLogFile = "combats/wrong.json" }],
                };
                break;
            case "run_id":
                logs = Logs(combat with { RunId = "another-run" });
                break;
            case "manifest_schema":
                manifest = manifest with { SchemaVersion = "1.1.0" };
                break;
            case "combat_schema":
                logs = Logs(combat with { SchemaVersion = "1.1.0" });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mismatch));
        }

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportWriter.WriteAsync(_outputDirectory, manifest, logs));

        Assert.False(Directory.Exists(_outputDirectory));
        Assert.Empty(ScratchDirectories());
    }

    [Theory]
    [InlineData("snow-\u96ea")]
    [InlineData("has space")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT9.log")]
    public async Task WriteAsync_RejectsNonPortableCombatIds(string combatId)
    {
        CombatLog combat = CreateCombatLog(combatId, floor: 1);

        await Assert.ThrowsAsync<ArgumentException>(() => ReportWriter.WriteAsync(
            _outputDirectory,
            CreateManifestFor(combat),
            Logs(combat)));

        Assert.False(Directory.Exists(_outputDirectory));
    }

    [Fact]
    public async Task WriteAsync_RejectsCombatIdsLongerThan128Characters()
    {
        string combatId = new('a', 129);
        CombatLog combat = CreateCombatLog(combatId, floor: 1);

        await Assert.ThrowsAsync<ArgumentException>(() => ReportWriter.WriteAsync(
            _outputDirectory,
            CreateManifestFor(combat),
            Logs(combat)));

        Assert.False(Directory.Exists(_outputDirectory));
    }

    [Fact]
    public async Task WriteAsync_Accepts128CharacterPortableCombatId()
    {
        string combatId = "a" + new string('b', 126) + "9";
        CombatLog combat = CreateCombatLog(combatId, floor: 1);

        await ReportWriter.WriteAsync(_outputDirectory, CreateManifestFor(combat), Logs(combat));

        Assert.True(File.Exists(Path.Combine(_outputDirectory, "combats", $"001_{combatId}.json")));
        Assert.Empty(ScratchDirectories());
    }

    [Theory]
    [InlineData("combat_id")]
    [InlineData("combat_log_file")]
    [InlineData("floor")]
    [InlineData("victory")]
    public async Task WriteAsync_RejectsCombatFloorDetailMismatchBeforeCreatingOutput(string mismatch)
    {
        CombatLog combat = CreateCombatLog("combat-001", floor: 1);
        RunManifest manifest = CreateManifestFor(combat);
        IReadOnlyDictionary<string, CombatLog> logs = Logs(combat);
        FloorEntry floor = manifest.Floors[0];
        var detail = Assert.IsType<CombatFloorDetail>(floor.Detail);
        string expectedMessage;

        switch (mismatch)
        {
            case "combat_id":
                manifest = manifest with
                {
                    Floors = [floor with { Detail = detail with { CombatId = "other" } }],
                };
                expectedMessage = "must link to exactly one indexed combat log";
                break;
            case "combat_log_file":
                manifest = manifest with
                {
                    Floors = [floor with { Detail = detail with { CombatLogFile = "combats/wrong.json" } }],
                };
                expectedMessage = "must match its index, floor, path, and result";
                break;
            case "floor":
                combat = combat with { Floor = 2 };
                logs = Logs(combat);
                manifest = manifest with
                {
                    CombatLogs = [new CombatLogRef(combat.CombatId, 2, "combats/002_combat-001.json")],
                };
                expectedMessage = "must match its index, floor, path, and result";
                break;
            case "victory":
                manifest = manifest with
                {
                    Floors = [floor with { Detail = detail with { Victory = false } }],
                };
                expectedMessage = "must match its index, floor, path, and result";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mismatch));
        }

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportWriter.WriteAsync(_outputDirectory, manifest, logs));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Equal("manifest", exception.ParamName);
        Assert.False(Directory.Exists(_outputDirectory));
        Assert.Empty(ScratchDirectories());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteAsync_AllowsIndexedForcedCombatOnNonCombatFloorDetail(bool ancient)
    {
        CombatLog combat = CreateCombatLog("forced", floor: 1);
        combat = combat with { EncounterName = CombatRoom.ForcedEncounterName };
        RunManifest manifest = CreateManifestFor(combat);
        FloorDetail detail = ancient
            ? new AncientFloorDetail("Neow", "Fight")
            : new EventFloorDetail("Arena", "Fight", "Forced combat");
        manifest = manifest with
        {
            Floors = [manifest.Floors[0] with { Detail = detail }],
        };

        await ReportWriter.WriteAsync(_outputDirectory, manifest, Logs(combat));

        Assert.True(File.Exists(Path.Combine(_outputDirectory, "combats", "001_forced.json")));
    }

    [Fact]
    public async Task WriteAsync_RejectsCaseInsensitiveCanonicalPathCollisionBeforeCreatingOutput()
    {
        CombatLog lower = CreateCombatLog("forced-a", floor: 1) with
        {
            EncounterName = CombatRoom.ForcedEncounterName,
        };
        CombatLog upper = CreateCombatLog("FORCED-A", floor: 1) with
        {
            EncounterName = CombatRoom.ForcedEncounterName,
        };
        RunManifest manifest = CreateNonCombatManifest() with
        {
            RunId = "run-雪",
            CombatLogs =
            [
                new CombatLogRef(lower.CombatId, 1, "combats/001_forced-a.json"),
                new CombatLogRef(upper.CombatId, 1, "combats/001_FORCED-A.json"),
            ],
        };
        var logs = new Dictionary<string, CombatLog>(StringComparer.Ordinal)
        {
            [lower.CombatId] = lower,
            [upper.CombatId] = upper,
        };

        ReportWriter.WriteFileAsync failAnyWrite = (_, _, _) =>
            Task.FromException(new IOException("Validation must happen before writing."));

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportWriter.WriteWithFileWriterAsync(_outputDirectory, manifest, logs, failAnyWrite));

        Assert.Contains("collides on a case-insensitive filesystem", exception.Message, StringComparison.Ordinal);
        Assert.Equal("manifest", exception.ParamName);
        Assert.False(Directory.Exists(_outputDirectory));
        Assert.Empty(ScratchDirectories());
    }

    [Theory]
    [InlineData("floors_visited")]
    [InlineData("floor_index")]
    [InlineData("result")]
    [InlineData("floors_null")]
    [InlineData("deck_before_null")]
    [InlineData("deck_after_null")]
    [InlineData("relics_before_null")]
    [InlineData("relics_after_null")]
    [InlineData("potions_before_null")]
    [InlineData("potions_after_null")]
    public async Task WriteAsync_RejectsInvalidManifestInvariantsBeforeCreatingOutput(string mismatch)
    {
        RunManifest manifest = CreateNonCombatManifest();
        FloorEntry floor = manifest.Floors[0];
        manifest = mismatch switch
        {
            "floors_visited" => manifest with { FloorsVisited = 2 },
            "floor_index" => manifest with { Floors = [floor with { FloorIndex = 2 }] },
            "result" => manifest with { Result = "incomplete" },
            "floors_null" => manifest with { Floors = null! },
            "deck_before_null" => manifest with { Floors = [floor with { DeckBefore = null! }] },
            "deck_after_null" => manifest with { Floors = [floor with { DeckAfter = null! }] },
            "relics_before_null" => manifest with { Floors = [floor with { RelicsBefore = null! }] },
            "relics_after_null" => manifest with { Floors = [floor with { RelicsAfter = null! }] },
            "potions_before_null" => manifest with { Floors = [floor with { PotionsBefore = null! }] },
            "potions_after_null" => manifest with { Floors = [floor with { PotionsAfter = null! }] },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => ReportWriter.WriteAsync(
            _outputDirectory,
            manifest,
            new Dictionary<string, CombatLog>(StringComparer.Ordinal)));

        Assert.False(Directory.Exists(_outputDirectory));
        Assert.Empty(ScratchDirectories());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteAsync_AllowsValidEmptyAndNonCombatRuns(bool empty)
    {
        RunManifest manifest = empty
            ? CreateNonCombatManifest() with { FloorsVisited = 0, Floors = [] }
            : CreateNonCombatManifest();

        await ReportWriter.WriteAsync(
            _outputDirectory,
            manifest,
            new Dictionary<string, CombatLog>(StringComparer.Ordinal));

        Assert.True(File.Exists(Path.Combine(_outputDirectory, "run_manifest.json")));
        Assert.Empty(Directory.GetFiles(Path.Combine(_outputDirectory, "combats")));
    }

    [Theory]
    [InlineData("coord", "coord")]
    [InlineData("detail", "detail")]
    [InlineData("turns", "turns")]
    [InlineData("actions", "actions")]
    [InlineData("cards_offered", "cards offered")]
    [InlineData("player_initial", "player_initial")]
    [InlineData("enemies_initial", "enemies_initial")]
    [InlineData("rewards", "rewards")]
    [InlineData("player_pre", "player_pre")]
    [InlineData("enemies_pre", "enemies_pre")]
    [InlineData("draws", "draws")]
    [InlineData("player_post", "player_post")]
    [InlineData("enemies_post", "enemies_post")]
    [InlineData("play_damage", "damage_dealt")]
    [InlineData("play_powers", "powers_applied")]
    [InlineData("potion_damage", "damage_dealt")]
    [InlineData("potion_powers", "powers_applied")]
    [InlineData("potions_added", "potions_added")]
    [InlineData("potions_removed", "potions_removed")]
    [InlineData("enemy_damage", "damage_to_targets")]
    [InlineData("enemy_powers", "powers_applied")]
    [InlineData("cards_taken", "cards taken")]
    [InlineData("relics_taken", "relics taken")]
    [InlineData("potions_taken", "potions taken")]
    [InlineData("draw_entry", "draws[0]")]
    [InlineData("action_entry", "actions[0]")]
    [InlineData("initial_power_entry", "powers[0]")]
    [InlineData("player_power_entry", "powers[0]")]
    public async Task WriteAsync_RejectsNullRequiredReportGraphMembers(
        string member,
        string expectedReason)
    {
        CombatLog combat = CreateCombatLog("combat-001", floor: 1);
        RunManifest manifest = CreateManifestFor(combat);
        FloorEntry floor = manifest.Floors[0];
        TurnRecord turn = combat.Turns[0];

        switch (member)
        {
            case "coord":
                manifest = manifest with { Floors = [floor with { Coord = null! }] };
                break;
            case "detail":
                manifest = manifest with { Floors = [floor with { Detail = null! }] };
                break;
            case "turns":
                combat = combat with { Turns = null! };
                break;
            case "actions":
                combat = combat with { Turns = [turn with { Actions = null! }] };
                break;
            case "cards_offered":
                combat = combat with { Rewards = combat.Rewards with { CardsOffered = null! } };
                break;
            case "player_initial":
                combat = combat with { PlayerInitial = null! };
                break;
            case "enemies_initial":
                combat = combat with { EnemiesInitial = null! };
                break;
            case "rewards":
                combat = combat with { Rewards = null! };
                break;
            case "player_pre":
                combat = combat with { Turns = [turn with { PlayerPre = null! }] };
                break;
            case "enemies_pre":
                combat = combat with { Turns = [turn with { EnemiesPre = null! }] };
                break;
            case "draws":
                combat = combat with { Turns = [turn with { Draws = null! }] };
                break;
            case "player_post":
                combat = combat with { Turns = [turn with { PlayerPost = null! }] };
                break;
            case "enemies_post":
                combat = combat with { Turns = [turn with { EnemiesPost = null! }] };
                break;
            case "play_damage":
                combat = combat with
                {
                    Turns = [turn with { Actions = [new PlayCardAction("card", null, 0, null!, [], 0)] }],
                };
                break;
            case "play_powers":
                combat = combat with
                {
                    Turns = [turn with { Actions = [new PlayCardAction("card", null, 0, [], null!, 0)] }],
                };
                break;
            case "potion_damage":
                combat = combat with
                {
                    Turns = [turn with { Actions = [new UsePotionAction("potion", null, null!, [])] }],
                };
                break;
            case "potion_powers":
                combat = combat with
                {
                    Turns = [turn with { Actions = [new UsePotionAction("potion", null, [], null!)] }],
                };
                break;
            case "potions_added":
                combat = combat with
                {
                    Turns = [turn with { Actions = [new UsePotionAction("potion", null, [], []) { PotionsAdded = null! }] }],
                };
                break;
            case "potions_removed":
                combat = combat with
                {
                    Turns = [turn with { Actions = [new UsePotionAction("potion", null, [], []) { PotionsRemoved = null! }] }],
                };
                break;
            case "enemy_damage":
                combat = combat with
                {
                    Turns = [turn with { Actions = [new EnemyAction("enemy", "move", null!, [])] }],
                };
                break;
            case "enemy_powers":
                combat = combat with
                {
                    Turns = [turn with { Actions = [new EnemyAction("enemy", "move", [], null!)] }],
                };
                break;
            case "cards_taken":
                combat = combat with { Rewards = combat.Rewards with { CardsTaken = null! } };
                break;
            case "relics_taken":
                combat = combat with { Rewards = combat.Rewards with { RelicsTaken = null! } };
                break;
            case "potions_taken":
                combat = combat with { Rewards = combat.Rewards with { PotionsTaken = null! } };
                break;
            case "draw_entry":
                combat = combat with { Turns = [turn with { Draws = [null!] }] };
                break;
            case "action_entry":
                combat = combat with { Turns = [turn with { Actions = [null!] }] };
                break;
            case "initial_power_entry":
                combat = combat with
                {
                    EnemiesInitial = [combat.EnemiesInitial[0] with { Powers = [null!] }],
                };
                break;
            case "player_power_entry":
                combat = combat with
                {
                    Turns = [turn with { PlayerPre = turn.PlayerPre with { Powers = [null!] } }],
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(member));
        }

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportWriter.WriteAsync(_outputDirectory, manifest, Logs(combat)));

        Assert.Contains(expectedReason, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(_outputDirectory));
        Assert.Empty(ScratchDirectories());
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    private static RunManifest CreateNonCombatManifest() => new(
        SchemaVersion: ReportingSchema.CurrentVersion,
        RunId: "run-non-combat",
        Seed: "non-combat",
        Character: "REGENT",
        Ascension: 0,
        StartedAtUtc: DateTimeOffset.UnixEpoch,
        EndedAtUtc: DateTimeOffset.UnixEpoch.AddMinutes(1),
        Result: "defeat",
        FinalHp: 70,
        MaxHp: 75,
        FloorsVisited: 1,
        Floors:
        [
            new FloorEntry(
                FloorIndex: 1,
                Coord: new MapCoordinate(0, 0),
                PointType: "Event",
                RoomType: "Event",
                HpBefore: 75,
                HpAfter: 70,
                MaxHp: 75,
                GoldBefore: 99,
                GoldAfter: 99,
                DeckBefore: ["CARD.STRIKE_REGENT"],
                DeckAfter: ["CARD.STRIKE_REGENT"],
                RelicsBefore: ["RELIC.DIVINE_RIGHT"],
                RelicsAfter: ["RELIC.DIVINE_RIGHT"],
                PotionsBefore: [null],
                PotionsAfter: [null],
                Detail: new EventFloorDetail("Arena", "Leave", "No effect"))
        ])
    {
        CombatLogs = [],
    };

    private static RunManifest CreateManifest(string result) => new(
        SchemaVersion: ReportingSchema.CurrentVersion,
        RunId: "run-雪",
        Seed: "42",
        Character: "REGENT",
        Ascension: 0,
        StartedAtUtc: DateTimeOffset.UnixEpoch,
        EndedAtUtc: DateTimeOffset.UnixEpoch.AddMinutes(1),
        Result: result,
        FinalHp: 70,
        MaxHp: 75,
        FloorsVisited: 1,
        Floors:
        [
            new FloorEntry(
                FloorIndex: 1,
                Coord: new MapCoordinate(1, 2),
                PointType: "Monster",
                RoomType: "Monster",
                HpBefore: 75,
                HpAfter: 70,
                MaxHp: 75,
                GoldBefore: 99,
                GoldAfter: 119,
                DeckBefore: ["CARD.STRIKE_REGENT"],
                DeckAfter: ["CARD.STRIKE_REGENT"],
                RelicsBefore: ["RELIC.DIVINE_RIGHT"],
                RelicsAfter: ["RELIC.DIVINE_RIGHT"],
                PotionsBefore: ["POTION.BLOOD_POTION"],
                PotionsAfter: [null],
                Detail: new CombatFloorDetail("combat-001", "combats/001_combat-001.json", true))
        ])
    {
        CombatLogs = [new CombatLogRef("combat-001", 1, "combats/001_combat-001.json")],
    };

    private static RunManifest CreateManifestFor(CombatLog combat, string result = "victory")
    {
        RunManifest manifest = CreateManifest(result);
        return manifest with
        {
            Floors =
            [
                manifest.Floors[0] with
                {
                    FloorIndex = combat.Floor,
                    Detail = new CombatFloorDetail(
                        combat.CombatId,
                        $"combats/{combat.Floor:D3}_{combat.CombatId}.json",
                        combat.Result == "victory"),
                },
            ],
            CombatLogs =
            [
                new CombatLogRef(
                    combat.CombatId,
                    combat.Floor,
                    $"combats/{combat.Floor:D3}_{combat.CombatId}.json"),
            ],
        };
    }

    private static IReadOnlyDictionary<string, CombatLog> Logs(params CombatLog[] combats) =>
        combats.ToDictionary(combat => combat.CombatId, StringComparer.Ordinal);

    private static ReportWriter.WriteFileAsync InterruptOnWrite(int writeNumber, Action interrupt)
    {
        int writes = 0;
        return async (path, contents, cancellationToken) =>
        {
            writes++;
            if (writes == writeNumber)
            {
                interrupt();
            }

            await File.WriteAllTextAsync(
                path,
                contents,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        };
    }

    private string[] ScratchDirectories()
    {
        string parent = Directory.GetParent(_outputDirectory)!.FullName;
        string pattern = $".{Path.GetFileName(_outputDirectory)}.staging-*";
        return Directory.Exists(parent)
            ? Directory.GetDirectories(parent, pattern)
            : [];
    }

    private static CombatLog CreateCombatLog(string combatId, int floor)
    {
        var player = new PlayerSnapshot(75, 75, 0, 3, 0, ["CARD.STRIKE_REGENT"], 4, 0, 0, []);
        var enemy = new EnemySnapshot("front", "MONSTER.FLYCONID", 25, 25, 0, [], "Attack 8");
        var potion = new UsePotionAction("POTION.BLOOD_POTION", null, [], [])
        {
            HealingReceived = 7,
            Consumed = true,
            PotionsRemoved = ["POTION.BLOOD_POTION"],
        };

        return new CombatLog(
            SchemaVersion: ReportingSchema.CurrentVersion,
            RunId: "run-雪",
            CombatId: combatId,
            Floor: floor,
            EncounterType: "Monster",
            EncounterName: "FlyconidNormal",
            StartedAtUtc: DateTimeOffset.UnixEpoch,
            EndedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(30),
            Result: "victory",
            PlayerInitial: new CombatPlayerInitialSnapshot(75, 75, 0, 3, 10, ["RELIC.DIVINE_RIGHT"], ["POTION.BLOOD_POTION"]),
            EnemiesInitial: [new CombatEnemyInitialSnapshot("front", "MONSTER.FLYCONID", 25, 25, [])],
            Turns: [new TurnRecord(1, "player", player, [enemy], [], [potion], player with { Hp = 75 }, [enemy with { Hp = 0 }])],
            Rewards: new CombatRewards(20, ["CARD.STRIKE_REGENT"], "CARD.STRIKE_REGENT", null, null)
            {
                CardsTaken = ["CARD.STRIKE_REGENT"],
            });
    }
}
