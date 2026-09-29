using System.Text.Json;
using Sts2Sim.Core.Reporting;

namespace Sts2Sim.Core.Tests.Reporting;

public class ReportingDtoSerializationTests
{
    [Fact]
    public void RunManifestAndCombatLog_RoundTripRepresentativeValues()
    {
        var startedAt = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        var endedAt = startedAt.AddMinutes(15);
        var manifest = new RunManifest(
            SchemaVersion: "1.2.0",
            RunId: "seed-123",
            Seed: "123",
            Character: "REGENT",
            Ascension: 2,
            StartedAtUtc: startedAt,
            EndedAtUtc: endedAt,
            Result: "victory",
            FinalHp: 61,
            MaxHp: 75,
            FloorsVisited: 1,
            Floors:
            [
                new FloorEntry(
                    FloorIndex: 1,
                    Coord: new MapCoordinate(Col: 3, Row: 0),
                    PointType: "Merchant",
                    RoomType: "Shop",
                    HpBefore: 75,
                    HpAfter: 75,
                    MaxHp: 75,
                    GoldBefore: 99,
                    GoldAfter: 249,
                    DeckBefore: ["STRIKE_REGENT"],
                    DeckAfter: ["STRIKE_REGENT", "DEFEND_REGENT"],
                    RelicsBefore: ["DIVINE_RIGHT"],
                    RelicsAfter: ["DIVINE_RIGHT", "GOLDEN_PEARL"],
                    PotionsBefore: [null, null],
                    PotionsAfter: ["FIRE_POTION", null],
                    Detail: new ShopFloorDetail([new PurchaseRecord("Card", "DEFEND_REGENT", 50)]))
            ])
        {
            CombatLogs = [new CombatLogRef("flyconid-1", 2, "combats/002_flyconid-1.json")],
        };

        var combatLog = new CombatLog(
            SchemaVersion: "1.2.0",
            RunId: "seed-123",
            CombatId: "flyconid-1",
            Floor: 2,
            EncounterType: "Monster",
            EncounterName: "FlyconidNormal",
            StartedAtUtc: startedAt,
            EndedAtUtc: endedAt,
            Result: "victory",
            PlayerInitial: new CombatPlayerInitialSnapshot(
                Hp: 75,
                MaxHp: 75,
                Block: 0,
                Energy: 3,
                DeckSize: 10,
                RelicIds: ["DIVINE_RIGHT"],
                PotionIds: ["FIRE_POTION", null]),
            EnemiesInitial:
            [
                new CombatEnemyInitialSnapshot("front", "Flyconid", 30, 30, [])
            ],
            Turns:
            [
                new TurnRecord(
                    TurnIndex: 1,
                    Side: "player",
                    PlayerPre: PlayerSnapshot(),
                    EnemiesPre: [EnemySnapshot()],
                    Draws: [new DrawRecord("STRIKE_REGENT")],
                    Actions:
                    [
                        new PlayCardAction("STRIKE_REGENT", "front", 1, [new DamageDealtRecord("front", 9)], [new PowerApplicationRecord("front", "Weak", 1)], 5, PlayCount: 3),
                        new UsePotionAction("FIRE_POTION", "front", [new DamageDealtRecord("front", 20)], [])
                        {
                            HealingReceived = 7,
                            Consumed = true,
                            PotionsAdded = ["POTION.DEXTERITY_POTION"],
                            PotionsRemoved = ["FIRE_POTION"],
                        },
                        new EnemyAction("front", "SWIPE_MOVE", [new DamageTakenRecord(8, 5)], []),
                        new EndTurnAction(),
                    ],
                    PlayerPost: PlayerSnapshot(hp: 67),
                    EnemiesPost: [EnemySnapshot(hp: 1)])
            ],
            Rewards: new CombatRewards(20, ["DEFEND_REGENT"], "DEFEND_REGENT", null, "FIRE_POTION")
            {
                CardsTaken = ["DEFEND_REGENT", "STRIKE_REGENT"],
                RelicsTaken = ["CIRCLET", "VAJRA"],
                PotionsTaken = ["FIRE_POTION", "DEXTERITY_POTION"],
            });

        RunManifest manifestRoundTrip = RoundTrip(manifest);
        CombatLog combatLogRoundTrip = RoundTrip(combatLog);

        Assert.Equal("1.2.0", manifestRoundTrip.SchemaVersion);
        Assert.Equal("seed-123", manifestRoundTrip.RunId);
        Assert.Equal("123", manifestRoundTrip.Seed);
        Assert.Equal("REGENT", manifestRoundTrip.Character);
        Assert.Equal(2, manifestRoundTrip.Ascension);
        Assert.Equal(startedAt, manifestRoundTrip.StartedAtUtc);
        Assert.Equal(endedAt, manifestRoundTrip.EndedAtUtc);
        Assert.Equal("victory", manifestRoundTrip.Result);
        Assert.Equal(61, manifestRoundTrip.FinalHp);
        Assert.Equal(75, manifestRoundTrip.MaxHp);
        Assert.Equal(1, manifestRoundTrip.FloorsVisited);
        FloorEntry manifestFloor = Assert.Single(manifestRoundTrip.Floors);
        Assert.Equal(1, manifestFloor.FloorIndex);
        Assert.Equal(3, manifestFloor.Coord.Col);
        Assert.Equal(0, manifestFloor.Coord.Row);
        Assert.Equal("Merchant", manifestFloor.PointType);
        Assert.Equal("Shop", manifestFloor.RoomType);
        Assert.Equal(75, manifestFloor.HpBefore);
        Assert.Equal(75, manifestFloor.HpAfter);
        Assert.Equal(75, manifestFloor.MaxHp);
        Assert.Equal(99, manifestFloor.GoldBefore);
        Assert.Equal(249, manifestFloor.GoldAfter);
        Assert.Equal(["STRIKE_REGENT"], manifestFloor.DeckBefore);
        Assert.Equal(["STRIKE_REGENT", "DEFEND_REGENT"], manifestFloor.DeckAfter);
        Assert.Equal(["DIVINE_RIGHT"], manifestFloor.RelicsBefore);
        Assert.Equal(["DIVINE_RIGHT", "GOLDEN_PEARL"], manifestFloor.RelicsAfter);
        Assert.Equal([null, null], manifestFloor.PotionsBefore);
        Assert.Equal(["FIRE_POTION", null], manifestFloor.PotionsAfter);
        var shopDetail = Assert.IsType<ShopFloorDetail>(manifestFloor.Detail);
        PurchaseRecord purchase = Assert.Single(shopDetail.Purchases);
        CombatLogRef combatRef = Assert.Single(manifestRoundTrip.CombatLogs);
        Assert.Equal("flyconid-1", combatRef.CombatId);
        Assert.Equal(2, combatRef.Floor);
        Assert.Equal("combats/002_flyconid-1.json", combatRef.CombatLogFile);
        Assert.Equal("Card", purchase.Kind);
        Assert.Equal("DEFEND_REGENT", purchase.Id);
        Assert.Equal(50, purchase.Price);

        Assert.Equal("1.2.0", combatLogRoundTrip.SchemaVersion);
        Assert.Equal("seed-123", combatLogRoundTrip.RunId);
        Assert.Equal("flyconid-1", combatLogRoundTrip.CombatId);
        Assert.Equal(2, combatLogRoundTrip.Floor);
        Assert.Equal("Monster", combatLogRoundTrip.EncounterType);
        Assert.Equal("FlyconidNormal", combatLogRoundTrip.EncounterName);
        Assert.Equal(startedAt, combatLogRoundTrip.StartedAtUtc);
        Assert.Equal(endedAt, combatLogRoundTrip.EndedAtUtc);
        Assert.Equal("victory", combatLogRoundTrip.Result);
        Assert.Equal(75, combatLogRoundTrip.PlayerInitial.Hp);
        Assert.Equal(75, combatLogRoundTrip.PlayerInitial.MaxHp);
        Assert.Equal(0, combatLogRoundTrip.PlayerInitial.Block);
        Assert.Equal(3, combatLogRoundTrip.PlayerInitial.Energy);
        Assert.Equal(10, combatLogRoundTrip.PlayerInitial.DeckSize);
        Assert.Equal(["DIVINE_RIGHT"], combatLogRoundTrip.PlayerInitial.RelicIds);
        Assert.Equal(["FIRE_POTION", null], combatLogRoundTrip.PlayerInitial.PotionIds);
        CombatEnemyInitialSnapshot initialEnemy = Assert.Single(combatLogRoundTrip.EnemiesInitial);
        Assert.Equal("front", initialEnemy.Slot);
        Assert.Equal("Flyconid", initialEnemy.Id);
        Assert.Equal(30, initialEnemy.Hp);
        Assert.Equal(30, initialEnemy.MaxHp);
        Assert.Empty(initialEnemy.Powers);

        TurnRecord turn = Assert.Single(combatLogRoundTrip.Turns);
        Assert.Equal(1, turn.TurnIndex);
        Assert.Equal("player", turn.Side);
        AssertPlayerSnapshot(turn.PlayerPre, hp: 75);
        AssertEnemySnapshot(Assert.Single(turn.EnemiesPre), hp: 30);
        Assert.Equal("STRIKE_REGENT", Assert.Single(turn.Draws).Card);

        var playCard = Assert.IsType<PlayCardAction>(turn.Actions[0]);
        Assert.Equal("STRIKE_REGENT", playCard.Card);
        Assert.Equal("front", playCard.Target);
        Assert.Equal(1, playCard.EnergyCost);
        DamageDealtRecord cardDamage = Assert.Single(playCard.DamageDealt);
        Assert.Equal("front", cardDamage.Target);
        Assert.Equal(9, cardDamage.Amount);
        PowerApplicationRecord cardPower = Assert.Single(playCard.PowersApplied);
        Assert.Equal("front", cardPower.Target);
        Assert.Equal("Weak", cardPower.Power);
        Assert.Equal(1, cardPower.Amount);
        Assert.Equal(5, playCard.BlockGained);
        Assert.Equal(3, playCard.PlayCount);

        var usePotion = Assert.IsType<UsePotionAction>(turn.Actions[1]);
        Assert.Equal("FIRE_POTION", usePotion.Potion);
        Assert.Equal("front", usePotion.Target);
        DamageDealtRecord potionDamage = Assert.Single(usePotion.DamageDealt);
        Assert.Equal("front", potionDamage.Target);
        Assert.Equal(20, potionDamage.Amount);
        Assert.Empty(usePotion.PowersApplied);
        Assert.Equal(7, usePotion.HealingReceived);
        Assert.True(usePotion.Consumed);
        Assert.Equal(["POTION.DEXTERITY_POTION"], usePotion.PotionsAdded);
        Assert.Equal(["FIRE_POTION"], usePotion.PotionsRemoved);

        var enemyAction = Assert.IsType<EnemyAction>(turn.Actions[2]);
        Assert.Equal("front", enemyAction.Source);
        Assert.Equal("SWIPE_MOVE", enemyAction.MoveId);
        DamageTakenRecord enemyDamage = Assert.Single(enemyAction.DamageToTargets);
        Assert.Equal(8, enemyDamage.Amount);
        Assert.Equal(5, enemyDamage.BlockAbsorbed);
        Assert.Empty(enemyAction.PowersApplied);
        Assert.IsType<EndTurnAction>(turn.Actions[3]);

        AssertPlayerSnapshot(turn.PlayerPost, hp: 67);
        AssertEnemySnapshot(Assert.Single(turn.EnemiesPost), hp: 1);
        Assert.Equal(20, combatLogRoundTrip.Rewards.Gold);
        Assert.Equal(["DEFEND_REGENT"], combatLogRoundTrip.Rewards.CardsOffered);
        Assert.Equal("DEFEND_REGENT", combatLogRoundTrip.Rewards.CardTaken);
        Assert.Null(combatLogRoundTrip.Rewards.RelicTaken);
        Assert.Equal("FIRE_POTION", combatLogRoundTrip.Rewards.PotionTaken);
        Assert.Equal(["DEFEND_REGENT", "STRIKE_REGENT"], combatLogRoundTrip.Rewards.CardsTaken);
        Assert.Equal(["CIRCLET", "VAJRA"], combatLogRoundTrip.Rewards.RelicsTaken);
        Assert.Equal(["FIRE_POTION", "DEXTERITY_POTION"], combatLogRoundTrip.Rewards.PotionsTaken);
    }

    [Fact]
    public void LegacyActionJson_UsesAdditiveDefaultsWhenNewFieldsAreAbsent()
    {
        const string legacyPlayCardJson = """
            {"Card":"STRIKE_REGENT","Target":"front","EnergyCost":1,"DamageDealt":[],"PowersApplied":[],"BlockGained":0}
            """;
        const string legacyPotionJson = """
            {"Potion":"FIRE_POTION","Target":"front","DamageDealt":[],"PowersApplied":[]}
            """;
        const string legacyRewardsJson = """
            {"Gold":25,"CardsOffered":["CARD.STRIKE_REGENT"],"CardTaken":"CARD.STRIKE_REGENT","RelicTaken":null,"PotionTaken":null}
            """;

        PlayCardAction play = JsonSerializer.Deserialize<PlayCardAction>(legacyPlayCardJson)!;
        UsePotionAction potion = JsonSerializer.Deserialize<UsePotionAction>(legacyPotionJson)!;
        CombatRewards rewards = JsonSerializer.Deserialize<CombatRewards>(legacyRewardsJson)!;

        Assert.Equal(1, play.PlayCount);
        Assert.Equal(0, potion.HealingReceived);
        Assert.False(potion.Consumed);
        Assert.Empty(potion.PotionsAdded);
        Assert.Empty(potion.PotionsRemoved);
        Assert.Empty(rewards.CardsTaken);
        Assert.Empty(rewards.RelicsTaken);
        Assert.Empty(rewards.PotionsTaken);
    }

    [Theory]
    [MemberData(nameof(FloorDetailCases))]
    public void FloorEntry_RoundTripsEveryFloorDetailVariant(string pointType, string roomType, FloorDetail detail)
    {
        var floor = new FloorEntry(
            FloorIndex: 7,
            Coord: new MapCoordinate(Col: 2, Row: 4),
            PointType: pointType,
            RoomType: roomType,
            HpBefore: 66,
            HpAfter: 60,
            MaxHp: 75,
            GoldBefore: 40,
            GoldAfter: 60,
            DeckBefore: ["STRIKE_REGENT"],
            DeckAfter: ["DEFEND_REGENT"],
            RelicsBefore: ["DIVINE_RIGHT"],
            RelicsAfter: ["CIRCLET"],
            PotionsBefore: [null],
            PotionsAfter: ["FIRE_POTION"],
            Detail: detail);

        FloorEntry roundTrip = RoundTrip(floor);

        Assert.Equal(7, roundTrip.FloorIndex);
        Assert.Equal(2, roundTrip.Coord.Col);
        Assert.Equal(4, roundTrip.Coord.Row);
        Assert.Equal(pointType, roundTrip.PointType);
        Assert.Equal(roomType, roundTrip.RoomType);
        AssertFloorDetail(roundTrip.Detail);
    }

    public static TheoryData<string, string, FloorDetail> FloorDetailCases => new()
    {
        { "Monster", "Monster", new CombatFloorDetail("combat-7", "combats/007_combat-7.json", true) },
        { "RestSite", "RestSite", new RestSiteFloorDetail("Heal", 22, "STRIKE_REGENT") },
        { "Merchant", "Shop", new ShopFloorDetail([new PurchaseRecord("Potion", "FIRE_POTION", 75)]) },
        { "Treasure", "Treasure", new TreasureFloorDetail(20, "CIRCLET") },
        { "Event", "Event", new EventFloorDetail("LuminousChoir", "OFFER_TRIBUTE", "Gained a relic") },
        { "Ancient", "Ancient", new AncientFloorDetail("Neow", "GoldenPearl") },
    };

    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void AssertFloorDetail(FloorDetail detail)
    {
        switch (detail)
        {
            case CombatFloorDetail combat:
                Assert.Equal("combat-7", combat.CombatId);
                Assert.Equal("combats/007_combat-7.json", combat.CombatLogFile);
                Assert.True(combat.Victory);
                break;
            case RestSiteFloorDetail restSite:
                Assert.Equal("Heal", restSite.Decision);
                Assert.Equal(22, restSite.HealAmount);
                Assert.Equal("STRIKE_REGENT", restSite.UpgradedCard);
                break;
            case ShopFloorDetail shop:
                PurchaseRecord purchase = Assert.Single(shop.Purchases);
                Assert.Equal("Potion", purchase.Kind);
                Assert.Equal("FIRE_POTION", purchase.Id);
                Assert.Equal(75, purchase.Price);
                break;
            case TreasureFloorDetail treasure:
                Assert.Equal(20, treasure.GoldGained);
                Assert.Equal("CIRCLET", treasure.RelicGained);
                break;
            case EventFloorDetail @event:
                Assert.Equal("LuminousChoir", @event.EventName);
                Assert.Equal("OFFER_TRIBUTE", @event.OptionChosen);
                Assert.Equal("Gained a relic", @event.EffectsSummary);
                break;
            case AncientFloorDetail ancient:
                Assert.Equal("Neow", ancient.EventName);
                Assert.Equal("GoldenPearl", ancient.OptionChosen);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(detail), detail, "Unexpected floor detail type.");
        }
    }

    private static void AssertPlayerSnapshot(PlayerSnapshot snapshot, int hp)
    {
        Assert.Equal(hp, snapshot.Hp);
        Assert.Equal(75, snapshot.MaxHp);
        Assert.Equal(0, snapshot.Block);
        Assert.Equal(3, snapshot.Energy);
        Assert.Equal(0, snapshot.Stars);
        Assert.Equal(["STRIKE_REGENT"], snapshot.Hand);
        Assert.Equal(4, snapshot.DrawPileSize);
        Assert.Equal(2, snapshot.DiscardPileSize);
        Assert.Equal(1, snapshot.ExhaustPileSize);
        Assert.Empty(snapshot.Powers);
    }

    private static void AssertEnemySnapshot(EnemySnapshot snapshot, int hp)
    {
        Assert.Equal("front", snapshot.Slot);
        Assert.Equal("Flyconid", snapshot.Id);
        Assert.Equal(hp, snapshot.Hp);
        Assert.Equal(30, snapshot.MaxHp);
        Assert.Equal(0, snapshot.Block);
        Assert.Empty(snapshot.Powers);
        Assert.Equal("Attack 9", snapshot.Intent);
    }

    private static PlayerSnapshot PlayerSnapshot(int hp = 75) => new(
        Hp: hp,
        MaxHp: 75,
        Block: 0,
        Energy: 3,
        Stars: 0,
        Hand: ["STRIKE_REGENT"],
        DrawPileSize: 4,
        DiscardPileSize: 2,
        ExhaustPileSize: 1,
        Powers: []);

    private static EnemySnapshot EnemySnapshot(int hp = 30) => new(
        Slot: "front",
        Id: "Flyconid",
        Hp: hp,
        MaxHp: 30,
        Block: 0,
        Powers: [],
        Intent: "Attack 9");
}
