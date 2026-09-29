using System.Globalization;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Reporting;

public sealed class MarkdownReportRendererTests
{
    [Fact]
    public void Render_IncludesRunFloorsAllActionsSnapshotsAndPluralRewards()
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> logs) = RepresentativeReport();

        string markdown = MarkdownReportRenderer.Render(manifest, logs);

        Assert.Contains("# Run Report: run-123", markdown, StringComparison.Ordinal);
        Assert.Contains("| Character | REGENT |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Result | victory |", markdown, StringComparison.Ordinal);
        Assert.Contains("| 1 | Event | Event | 75 → 68 / 75 | 99 → 119 | event=Arena; option=Fight; effects=Forced combat |", markdown, StringComparison.Ordinal);
        Assert.Contains("| 3 | RestSite | RestSite | 50 → 72 / 75 | 119 → 119 | decision=Heal; healing=22; upgraded=none |", markdown, StringComparison.Ordinal);
        Assert.Contains("| 4 | Merchant | Shop | 72 → 72 / 75 | 119 → 44 | purchases=Potion:FIRE_POTION@75 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| 5 | Treasure | Treasure | 72 → 72 / 75 | 44 → 64 | gold=20; relic=CIRCLET |", markdown, StringComparison.Ordinal);
        Assert.Contains("| 6 | Event | Event | 72 → 72 / 75 | 64 → 64 | event=Shrine; option=Pray; effects=Gained strength |", markdown, StringComparison.Ordinal);
        Assert.Contains("| 7 | Ancient | Ancient | 72 → 72 / 75 | 64 → 64 | ancient=Neow; option=GoldenPearl |", markdown, StringComparison.Ordinal);

        Assert.Contains("### Floor 1 — Monster / forced-combat (forced-1)", markdown, StringComparison.Ordinal);
        Assert.Contains("duration: 00:01:00", markdown, StringComparison.Ordinal);
        Assert.Contains("#### Turn 1 (player)", markdown, StringComparison.Ordinal);
        Assert.Contains("Pre: player hp=75/75, block=0, energy=3, stars=2", markdown, StringComparison.Ordinal);
        Assert.Contains("Draws: STRIKE_REGENT, DEFEND_REGENT", markdown, StringComparison.Ordinal);
        Assert.Contains("play_card — actor=player; card=STRIKE_REGENT; target=front; plays=2; energy_cost=1; damage=front:9; block_gained=5; powers=front:Weak+1", markdown, StringComparison.Ordinal);
        Assert.Contains("use_potion — actor=player; potion=BLOOD_POTION; target=self; healing=7; consumed=yes; damage=none; powers=self:Strength+2; potions_added=DEXTERITY_POTION; potions_removed=BLOOD_POTION", markdown, StringComparison.Ordinal);
        Assert.Contains("enemy_action — actor=front; move=SWIPE_MOVE; damage_to_player=8 (block_absorbed=5); powers=player:Weak+1", markdown, StringComparison.Ordinal);
        Assert.Contains("end_turn — actor=player", markdown, StringComparison.Ordinal);
        Assert.Contains("Post: player hp=67/75, block=0, energy=2, stars=1", markdown, StringComparison.Ordinal);
        Assert.Contains("Rewards: gold=25; offered=DEFEND_REGENT, BASH_REGENT; cards_taken=DEFEND_REGENT, BASH_REGENT; relics_taken=CIRCLET, VAJRA; potions_taken=FIRE_POTION, DEXTERITY_POTION", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_UsesManifestIndexForForcedCombatsAndStableOrdering()
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> logs) = RepresentativeReport();
        manifest = manifest with
        {
            CombatLogs = manifest.CombatLogs.Reverse().ToArray(),
        };
        logs = new Dictionary<string, CombatLog>(StringComparer.Ordinal)
        {
            ["normal-2"] = logs["normal-2"],
            ["forced-1"] = logs["forced-1"],
        };

        string markdown = MarkdownReportRenderer.Render(manifest, logs);

        int forced = markdown.IndexOf("### Floor 1 — Monster / forced-combat (forced-1)", StringComparison.Ordinal);
        int normal = markdown.IndexOf("### Floor 2 — Monster / FlyconidNormal (normal-2)", StringComparison.Ordinal);
        Assert.True(forced >= 0, "The indexed Event forced combat should be rendered.");
        Assert.True(normal > forced, "Combats should be ordered by floor and then combat ID.");
    }

    [Fact]
    public void Render_EscapesMarkdownAndIsCultureInvariant()
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> logs) = RepresentativeReport();
        manifest = manifest with
        {
            Character = "REG|ENT\n# title *bold* `code` [link] <tag>",
            Floors =
            [
                manifest.Floors[0] with
                {
                    Detail = new EventFloorDetail("A|rena", "Fight\nNow", "#bad *effect*"),
                },
            ],
        };
        logs = new Dictionary<string, CombatLog>(StringComparer.Ordinal)
        {
            ["forced-1"] = logs["forced-1"],
        };
        manifest = manifest with
        {
            FloorsVisited = 1,
            CombatLogs = [manifest.CombatLogs[0]],
        };

        string english;
        string turkish;
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            english = MarkdownReportRenderer.Render(manifest, logs);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            turkish = MarkdownReportRenderer.Render(manifest, logs);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        Assert.Equal(english, turkish);
        Assert.Equal("| Character | REG&#124;ENT<br>&#35; title &#42;bold&#42; &#96;code&#96; &#91;link&#93; &lt;tag&gt; |", english.Split('\n').Single(line => line.StartsWith("| Character", StringComparison.Ordinal)));
        Assert.Contains("event=A&#124;rena; option=Fight<br>Now; effects=&#35;bad &#42;effect&#42;", english, StringComparison.Ordinal);
        Assert.Contains("STRIKE_REGENT", english, StringComparison.Ordinal);
        Assert.DoesNotContain("\n# title", english, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_IncludesCompleteFloorInventoryTransitionsAndEmptyPotionSlots()
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> logs) = RepresentativeReport();
        FloorEntry first = manifest.Floors[0] with
        {
            DeckBefore = ["CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT"],
            DeckAfter = ["CARD.STRIKE_REGENT", "CARD.DEFEND_REGENT"],
            RelicsBefore = ["RELIC.DIVINE_RIGHT"],
            RelicsAfter = ["RELIC.DIVINE_RIGHT", "RELIC.CIRCLET"],
            PotionsBefore = [null, "POTION.BLOOD_POTION", null],
            PotionsAfter = ["POTION.FIRE_POTION", null, null],
        };
        manifest = manifest with { Floors = [first, .. manifest.Floors.Skip(1)] };

        string markdown = MarkdownReportRenderer.Render(manifest, logs);

        Assert.Contains("### Floor 1 inventory", markdown, StringComparison.Ordinal);
        Assert.Contains("Deck: CARD.STRIKE_REGENT, CARD.STRIKE_REGENT", markdown, StringComparison.Ordinal);
        Assert.Contains("CARD.STRIKE_REGENT, CARD.DEFEND_REGENT", markdown, StringComparison.Ordinal);
        Assert.Contains("Relics: RELIC.DIVINE_RIGHT", markdown, StringComparison.Ordinal);
        Assert.Contains("RELIC.DIVINE_RIGHT, RELIC.CIRCLET", markdown, StringComparison.Ordinal);
        Assert.Contains("Potions: [empty, POTION.BLOOD_POTION, empty]", markdown, StringComparison.Ordinal);
        Assert.Contains("[POTION.FIRE_POTION, empty, empty]", markdown, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("legacy_schema")]
    [InlineData("bad_index_path")]
    [InlineData("bad_detail_path")]
    [InlineData("bad_detail_victory")]
    [InlineData("duplicate_detail")]
    [InlineData("unlinked_normal_combat")]
    public void Render_RejectsTheSameInvalidReportSetsAsJsonWriter(string mismatch)
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> originalLogs) = RepresentativeReport();
        var logs = new Dictionary<string, CombatLog>(originalLogs, StringComparer.Ordinal);
        CombatLog normal = logs["normal-2"];
        FloorEntry normalFloor = manifest.Floors.Single(floor => floor.FloorIndex == 2);
        var normalDetail = Assert.IsType<CombatFloorDetail>(normalFloor.Detail);

        switch (mismatch)
        {
            case "legacy_schema":
                manifest = manifest with { SchemaVersion = "1.1.0" };
                logs["forced-1"] = logs["forced-1"] with { SchemaVersion = "1.1.0" };
                logs["normal-2"] = normal with { SchemaVersion = "1.1.0" };
                break;
            case "bad_index_path":
                manifest = manifest with
                {
                    CombatLogs =
                    [
                        manifest.CombatLogs[0],
                        manifest.CombatLogs[1] with { CombatLogFile = "combats/wrong.json" },
                    ],
                };
                break;
            case "bad_detail_path":
                manifest = ReplaceNormalFloor(
                    manifest,
                    normalFloor with
                    {
                        Detail = normalDetail with { CombatLogFile = "combats/wrong.json" },
                    });
                break;
            case "bad_detail_victory":
                manifest = ReplaceNormalFloor(
                    manifest,
                    normalFloor with { Detail = normalDetail with { Victory = false } });
                break;
            case "duplicate_detail":
                manifest = manifest with
                {
                    FloorsVisited = 8,
                    Floors = [.. manifest.Floors, normalFloor with { FloorIndex = 8 }],
                };
                break;
            case "unlinked_normal_combat":
                manifest = ReplaceNormalFloor(
                    manifest,
                    normalFloor with { Detail = new EventFloorDetail("Arena", "Fight", "none") });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null);
        }

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            MarkdownReportRenderer.Render(manifest, logs));
        if (mismatch == "duplicate_detail")
        {
            Assert.Contains("must link to exactly one indexed combat log", exception.Message, StringComparison.Ordinal);
            Assert.Equal("manifest", exception.ParamName);
        }
    }

    [Fact]
    public void Render_RejectsNegativeCombatTimeWindow()
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> originalLogs) = RepresentativeReport();
        var logs = new Dictionary<string, CombatLog>(originalLogs, StringComparer.Ordinal);
        CombatLog combat = logs["forced-1"];
        logs[combat.CombatId] = combat with { EndedAtUtc = combat.StartedAtUtc.AddTicks(-1) };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            MarkdownReportRenderer.Render(manifest, logs));

        Assert.Contains("ended", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_AllowsMultipleIndexedForcedCombatsOnOneEventFloor()
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> originalLogs) = RepresentativeReport();
        CombatLog second = originalLogs["forced-1"] with { CombatId = "forced-1b" };
        manifest = manifest with
        {
            CombatLogs =

            [
                .. manifest.CombatLogs,
                new CombatLogRef("forced-1b", 1, "combats/001_forced-1b.json"),
            ],
        };
        var logs = new Dictionary<string, CombatLog>(originalLogs, StringComparer.Ordinal)
        {
            [second.CombatId] = second,
        };

        string markdown = MarkdownReportRenderer.Render(manifest, logs);

        Assert.Contains("(forced-1)", markdown, StringComparison.Ordinal);
        Assert.Contains("(forced-1b)", markdown, StringComparison.Ordinal);
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
    public void Render_RejectsInvalidManifestInvariants(string mismatch)
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> logs) = RepresentativeReport();
        FloorEntry first = manifest.Floors[0];
        FloorEntry third = manifest.Floors[2];
        manifest = mismatch switch
        {
            "floors_visited" => manifest with { FloorsVisited = 8 },
            "floor_index" => manifest with
            {
                Floors = [first, manifest.Floors[1], third with { FloorIndex = 9 }, .. manifest.Floors.Skip(3)],
            },
            "result" => manifest with { Result = "incomplete" },
            "floors_null" => manifest with { Floors = null! },
            "deck_before_null" => ReplaceFirstFloor(manifest, first with { DeckBefore = null! }),
            "deck_after_null" => ReplaceFirstFloor(manifest, first with { DeckAfter = null! }),
            "relics_before_null" => ReplaceFirstFloor(manifest, first with { RelicsBefore = null! }),
            "relics_after_null" => ReplaceFirstFloor(manifest, first with { RelicsAfter = null! }),
            "potions_before_null" => ReplaceFirstFloor(manifest, first with { PotionsBefore = null! }),
            "potions_after_null" => ReplaceFirstFloor(manifest, first with { PotionsAfter = null! }),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };

        Assert.Throws<ArgumentException>(() => MarkdownReportRenderer.Render(manifest, logs));
    }

    [Fact]
    public void Render_HandlesEmptyCollectionsAndRejectsMissingIndexedCombat()
    {
        RunManifest empty = EmptyManifest();

        string markdown = MarkdownReportRenderer.Render(
            empty,
            new Dictionary<string, CombatLog>(StringComparer.Ordinal));

        Assert.Contains("| Floors visited | 0 |", markdown, StringComparison.Ordinal);
        Assert.Contains("_No floors visited._", markdown, StringComparison.Ordinal);
        Assert.Contains("_No combats recorded._", markdown, StringComparison.Ordinal);

        (RunManifest manifest, _) = RepresentativeReport();
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            MarkdownReportRenderer.Render(
                manifest,
                new Dictionary<string, CombatLog>(StringComparer.Ordinal)));
        Assert.Contains("forced-1", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("coord", "coord")]
    [InlineData("detail", "detail")]
    [InlineData("turns", "turns")]
    [InlineData("actions", "actions")]
    [InlineData("cards_offered", "cards offered")]
    public void Render_RejectsNullRequiredReportGraphMembers(
        string member,
        string expectedReason)
    {
        (RunManifest manifest, IReadOnlyDictionary<string, CombatLog> originalLogs) = RepresentativeReport();
        var logs = new Dictionary<string, CombatLog>(originalLogs, StringComparer.Ordinal);
        FloorEntry first = manifest.Floors[0];
        CombatLog combat = logs["forced-1"];
        TurnRecord turn = combat.Turns[0];

        switch (member)
        {
            case "coord":
                manifest = ReplaceFirstFloor(manifest, first with { Coord = null! });
                break;
            case "detail":
                manifest = ReplaceFirstFloor(manifest, first with { Detail = null! });
                break;
            case "turns":
                logs[combat.CombatId] = combat with { Turns = null! };
                break;
            case "actions":
                logs[combat.CombatId] = combat with { Turns = [turn with { Actions = null! }] };
                break;
            case "cards_offered":
                logs[combat.CombatId] = combat with
                {
                    Rewards = combat.Rewards with { CardsOffered = null! },
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(member));
        }

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            MarkdownReportRenderer.Render(manifest, logs));

        Assert.Contains(expectedReason, exception.Message, StringComparison.OrdinalIgnoreCase);
    }


    private static RunManifest ReplaceFirstFloor(RunManifest manifest, FloorEntry replacement) =>
        manifest with
        {
            Floors = [replacement, .. manifest.Floors.Skip(1)],
        };

    private static RunManifest ReplaceNormalFloor(RunManifest manifest, FloorEntry replacement) =>
        manifest with
        {
            Floors = manifest.Floors.Select(
                floor => floor.FloorIndex == 2 ? replacement : floor).ToArray(),
        };
    private static (RunManifest Manifest, IReadOnlyDictionary<string, CombatLog> Logs) RepresentativeReport()
    {
        DateTimeOffset started = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        CombatLog forced = Combat(
            "forced-1",
            floor: 1,
            encounterType: "Monster",
            encounterName: CombatRoom.ForcedEncounterName,
            started);
        CombatLog normal = Combat(
            "normal-2",
            floor: 2,
            encounterType: "Monster",
            encounterName: "FlyconidNormal",
            started.AddMinutes(2)) with
        {
            Turns = [],
            Rewards = new CombatRewards(0, [], null, null, null),
        };

        var manifest = new RunManifest(
            ReportingSchema.CurrentVersion,
            "run-123",
            "seed-123",
            "REGENT",
            3,
            started,
            started.AddMinutes(15),
            "victory",
            72,
            75,
            7,
            [
                Floor(1, "Event", "Event", 75, 68, 99, 119, new EventFloorDetail("Arena", "Fight", "Forced combat")),
                Floor(2, "Monster", "Monster", 68, 50, 119, 119, new CombatFloorDetail("normal-2", "combats/002_normal-2.json", true)),
                Floor(3, "RestSite", "RestSite", 50, 72, 119, 119, new RestSiteFloorDetail("Heal", 22, null)),
                Floor(4, "Merchant", "Shop", 72, 72, 119, 44, new ShopFloorDetail([new PurchaseRecord("Potion", "FIRE_POTION", 75)])),
                Floor(5, "Treasure", "Treasure", 72, 72, 44, 64, new TreasureFloorDetail(20, "CIRCLET")),
                Floor(6, "Event", "Event", 72, 72, 64, 64, new EventFloorDetail("Shrine", "Pray", "Gained strength")),
                Floor(7, "Ancient", "Ancient", 72, 72, 64, 64, new AncientFloorDetail("Neow", "GoldenPearl")),
            ])
        {
            CombatLogs =
            [
                new CombatLogRef("forced-1", 1, "combats/001_forced-1.json"),
                new CombatLogRef("normal-2", 2, "combats/002_normal-2.json"),
            ],
        };

        IReadOnlyDictionary<string, CombatLog> logs = new Dictionary<string, CombatLog>(StringComparer.Ordinal)
        {
            [forced.CombatId] = forced,
            [normal.CombatId] = normal,
        };
        return (manifest, logs);
    }

    private static CombatLog Combat(
        string combatId,
        int floor,
        string encounterType,
        string encounterName,
        DateTimeOffset started) => new(
        ReportingSchema.CurrentVersion,
        "run-123",
        combatId,
        floor,
        encounterType,
        encounterName,
        started,
        started.AddMinutes(1),
        "victory",
        new CombatPlayerInitialSnapshot(75, 75, 0, 3, 10, ["DIVINE_RIGHT"], ["BLOOD_POTION"]),
        [new CombatEnemyInitialSnapshot("front", "Flyconid", 30, 30, [new PowerSnapshot("Minion", 1)])],
        [
            new TurnRecord(
                1,
                "player",
                Player(hp: 75, energy: 3, stars: 2),
                [Enemy(hp: 30)],
                [new DrawRecord("STRIKE_REGENT"), new DrawRecord("DEFEND_REGENT")],
                [
                    new PlayCardAction(
                        "STRIKE_REGENT",
                        "front",
                        1,
                        [new DamageDealtRecord("front", 9)],
                        [new PowerApplicationRecord("front", "Weak", 1)],
                        5,
                        PlayCount: 2),
                    new UsePotionAction(
                        "BLOOD_POTION",
                        "self",
                        [],
                        [new PowerApplicationRecord("self", "Strength", 2)])
                    {
                        HealingReceived = 7,
                        Consumed = true,
                        PotionsAdded = ["DEXTERITY_POTION"],
                        PotionsRemoved = ["BLOOD_POTION"],
                    },
                    new EnemyAction(
                        "front",
                        "SWIPE_MOVE",
                        [new DamageTakenRecord(8, 5)],
                        [new PowerApplicationRecord("player", "Weak", 1)]),
                    new EndTurnAction(),
                ],
                Player(hp: 67, energy: 2, stars: 1),
                [Enemy(hp: 21)])
        ],
        new CombatRewards(25, ["DEFEND_REGENT", "BASH_REGENT"], "DEFEND_REGENT", "CIRCLET", "FIRE_POTION")
        {
            CardsTaken = ["DEFEND_REGENT", "BASH_REGENT"],
            RelicsTaken = ["CIRCLET", "VAJRA"],
            PotionsTaken = ["FIRE_POTION", "DEXTERITY_POTION"],
        });

    private static RunManifest EmptyManifest()
    {
        DateTimeOffset timestamp = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        return new RunManifest(
            ReportingSchema.CurrentVersion,
            "empty-run",
            "empty-seed",
            "REGENT",
            0,
            timestamp,
            timestamp,
            "defeat",
            75,
            75,
            0,
            []);
    }

    private static FloorEntry Floor(
        int floor,
        string pointType,
        string roomType,
        int hpBefore,
        int hpAfter,
        int goldBefore,
        int goldAfter,
        FloorDetail detail) => new(
        floor,
        new MapCoordinate(floor % 4, floor - 1),
        pointType,
        roomType,
        hpBefore,
        hpAfter,
        75,
        goldBefore,
        goldAfter,
        ["STRIKE_REGENT"],
        ["STRIKE_REGENT"],
        ["DIVINE_RIGHT"],
        ["DIVINE_RIGHT"],
        [null],
        [null],
        detail);

    private static PlayerSnapshot Player(int hp, int energy, int stars) => new(
        hp,
        75,
        0,
        energy,
        stars,
        ["STRIKE_REGENT"],
        4,
        2,
        1,
        [new PowerSnapshot("Strength", 2)]);

    private static EnemySnapshot Enemy(int hp) => new(
        "front",
        "Flyconid",
        hp,
        30,
        0,
        [new PowerSnapshot("Weak", 1)],
        "Attack 9");
}
