using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Reporting;

[Collection("ModelDb")]
public sealed class RunRecorderTests : IDisposable
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    public RunRecorderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
        [
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(StrengthPower),
            typeof(DivineRight),
            typeof(StrengthPotion),
            typeof(TrainingDummy),
        ]);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CompletedLifecycle_BuildsManifestAndCombatLogFromRecordedSnapshots()
    {
        DateTimeOffset now = StartedAt;
        var recorder = new RunRecorder(() => now);
        IRunRecorder contract = recorder;
        (RunState runState, Player player, CombatState combatState, Creature enemy) = CreateCombat("Report / Seed:One");
        var point = new MapPoint(col: 2, row: 3) { PointType = MapPointType.Monster };

        contract.BeginRun(runState);
        contract.EnterFloor(point, RoomType.Monster);
        now = StartedAt.AddMinutes(1);
        string combatId = contract.BeginCombat(
            runState,
            RoomType.Monster,
            "Training Dummy / Intro",
            combatState);

        contract.RecordTurnStart(combatState);
        CardModel strike = player.Deck.Cards.OfType<StrikeRegent>().First();
        contract.RecordDraw(strike);

        PlayerSnapshot playerPre = PlayerSnapshot(hp: 75, block: 0, energy: 3);
        EnemySnapshot enemyPre = EnemySnapshot(hp: 20, block: 0);
        PlayerSnapshot playerAfterCard = PlayerSnapshot(hp: 75, block: 5, energy: 2);
        EnemySnapshot enemyAfterCard = EnemySnapshot(hp: 14, block: 0);
        contract.RecordCardPlay(
            new CardPlay
            {
                Card = strike,
                Player = player,
                Target = enemy,
                ResultPile = PileType.Discard,
                Resources = new ResourceInfo(1, 1, 0, 0),
                IsAutoPlay = false,
                PlayIndex = 0,
                PlayCount = 1,
            },
            playerPre,
            playerAfterCard,
            [enemyPre],
            [enemyAfterCard]);

        PotionModel potion = ModelDb.Potion<StrengthPotion>();
        PlayerSnapshot playerAfterPotion = PlayerSnapshot(
            hp: 75,
            block: 5,
            energy: 2,
            powers: [new PowerSnapshot("POWER.STRENGTH_POWER", 2)]);
        contract.RecordPotionUse(
            potion,
            target: null,
            playerAfterCard,
            playerAfterPotion,
            [enemyAfterCard],
            [enemyAfterCard]);

        PlayerSnapshot playerAfterEnemy = PlayerSnapshot(
            hp: 70,
            block: 0,
            energy: 2,
            powers: [new PowerSnapshot("POWER.STRENGTH_POWER", 2)]);
        contract.RecordEnemyAction(
            enemy,
            "TRAINING_DUMMY_MOVE",
            [new ActionSnapshotSegment(
                playerAfterPotion,
                playerAfterEnemy,
                [enemyAfterCard],
                [enemyAfterCard])]);
        contract.RecordEndTurn(playerAfterEnemy, [enemyAfterCard]);

        now = StartedAt.AddMinutes(2);
        contract.EndCombat(
            victory: true,
            playerAfterEnemy,
            [enemyAfterCard],
            new CombatRewards(0, [], null, null, null));
        player.Creature.LoseHpInternal(5m, (ValueProp)0);
        player.Gold += 10;
        contract.ExitFloor();
        now = StartedAt.AddMinutes(3);
        contract.EndRun(won: true, floorsVisited: 1, finalHp: 70);

        RunManifest manifest = contract.BuildManifest();
        Assert.Equal("1.2.0", manifest.SchemaVersion);
        Assert.Matches("^[A-Za-z0-9_-]+$", manifest.RunId);
        Assert.Equal("Report / Seed:One", manifest.Seed);
        Assert.Equal("REGENT", manifest.Character);
        Assert.Equal(2, manifest.Ascension);
        Assert.Equal(StartedAt, manifest.StartedAtUtc);
        Assert.Equal(StartedAt.AddMinutes(3), manifest.EndedAtUtc);
        Assert.Equal("victory", manifest.Result);
        Assert.Equal(70, manifest.FinalHp);
        Assert.Equal(75, manifest.MaxHp);
        Assert.Equal(1, manifest.FloorsVisited);

        FloorEntry floor = Assert.Single(manifest.Floors);
        Assert.Equal(1, floor.FloorIndex);
        Assert.Equal(new MapCoordinate(2, 3), floor.Coord);
        Assert.Equal("Monster", floor.PointType);
        Assert.Equal("Monster", floor.RoomType);
        Assert.Equal(75, floor.HpBefore);
        Assert.Equal(70, floor.HpAfter);
        Assert.Equal(99, floor.GoldBefore);
        Assert.Equal(109, floor.GoldAfter);
        var combatDetail = Assert.IsType<CombatFloorDetail>(floor.Detail);
        Assert.Equal(combatId, combatDetail.CombatId);
        Assert.Equal($"combats/001_{combatId}.json", combatDetail.CombatLogFile);
        Assert.True(combatDetail.Victory);

        CombatLogRef combatRef = Assert.Single(manifest.CombatLogs);
        Assert.Equal(combatId, combatRef.CombatId);
        Assert.Equal(1, combatRef.Floor);
        Assert.Equal($"combats/001_{combatId}.json", combatRef.CombatLogFile);
        Assert.Same(contract.CombatLogs[combatId], recorder.BuildCombatLog(combatId));
        CombatLog combat = Assert.Single(contract.CombatLogs).Value;
        Assert.Equal("1.2.0", combat.SchemaVersion);
        Assert.Equal(manifest.RunId, combat.RunId);
        Assert.Equal("combat-001", combat.CombatId);
        Assert.Equal(1, combat.Floor);
        Assert.Equal("Monster", combat.EncounterType);
        Assert.Equal("Training Dummy / Intro", combat.EncounterName);
        Assert.Equal(StartedAt.AddMinutes(1), combat.StartedAtUtc);
        Assert.Equal(StartedAt.AddMinutes(2), combat.EndedAtUtc);
        Assert.Equal("victory", combat.Result);
        Assert.Equal(75, combat.PlayerInitial.Hp);
        Assert.Equal(3, combat.PlayerInitial.Energy);
        Assert.Equal(10, combat.PlayerInitial.DeckSize);
        Assert.Equal(["RELIC.DIVINE_RIGHT"], combat.PlayerInitial.RelicIds);
        Assert.Equal([null, null, null], combat.PlayerInitial.PotionIds);
        Assert.Equal("MONSTER.TRAINING_DUMMY", Assert.Single(combat.EnemiesInitial).Id);

        TurnRecord turn = Assert.Single(combat.Turns);
        Assert.Equal(1, turn.TurnIndex);
        Assert.Equal("player", turn.Side);
        Assert.Equal("CARD.STRIKE_REGENT", Assert.Single(turn.Draws).Card);
        AssertPlayerSnapshot(playerPre, turn.PlayerPre);
        AssertPlayerSnapshot(playerAfterEnemy, turn.PlayerPost);
        AssertEnemySnapshot(enemyPre, Assert.Single(turn.EnemiesPre));
        AssertEnemySnapshot(enemyAfterCard, Assert.Single(turn.EnemiesPost));

        var cardAction = Assert.IsType<PlayCardAction>(turn.Actions[0]);
        Assert.Equal("CARD.STRIKE_REGENT", cardAction.Card);
        Assert.Equal("front", cardAction.Target);
        Assert.Equal(1, cardAction.EnergyCost);
        Assert.Equal(new DamageDealtRecord("front", 6), Assert.Single(cardAction.DamageDealt));
        Assert.Empty(cardAction.PowersApplied);
        Assert.Equal(5, cardAction.BlockGained);

        var potionAction = Assert.IsType<UsePotionAction>(turn.Actions[1]);
        Assert.Equal("POTION.STRENGTH_POTION", potionAction.Potion);
        Assert.Null(potionAction.Target);
        Assert.Empty(potionAction.DamageDealt);
        Assert.Equal(
            new PowerApplicationRecord("player", "POWER.STRENGTH_POWER", 2),
            Assert.Single(potionAction.PowersApplied));

        var enemyAction = Assert.IsType<EnemyAction>(turn.Actions[2]);
        Assert.Equal("front", enemyAction.Source);
        Assert.Equal("TRAINING_DUMMY_MOVE", enemyAction.MoveId);
        Assert.Equal(new DamageTakenRecord(10, 5), Assert.Single(enemyAction.DamageToTargets));
        Assert.Empty(enemyAction.PowersApplied);
        Assert.IsType<EndTurnAction>(turn.Actions[3]);
        Assert.Equal(0, combat.Rewards.Gold);
        Assert.Empty(combat.Rewards.CardsOffered);
        Assert.Empty(combat.Rewards.CardsTaken);
        Assert.Empty(combat.Rewards.RelicsTaken);
        Assert.Empty(combat.Rewards.PotionsTaken);
        Assert.Null(combat.Rewards.CardTaken);
        Assert.Null(combat.Rewards.RelicTaken);
        Assert.Null(combat.Rewards.PotionTaken);

        string markdown = MarkdownReportRenderer.Render(manifest, recorder.CombatLogs);
        Assert.Contains("Training Dummy / Intro", markdown, StringComparison.Ordinal);
        Assert.Contains("play_card", markdown, StringComparison.Ordinal);
        Assert.Contains("use_potion", markdown, StringComparison.Ordinal);
        Assert.Contains("enemy_action", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordEnemyAction_SegmentsPreserveGrossDamageAndAggregatePowerApplications()
    {
        var recorder = new RunRecorder();
        (RunState runState, Player player, CombatState combatState, Creature enemy) =
            CreateCombat("segmented-enemy-action");
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        string combatId = recorder.BeginCombat(
            runState,
            RoomType.Monster,
            "TrainingDummy",
            combatState);
        recorder.RecordTurnStart(combatState);

        EnemySnapshot enemySnapshot = EnemySnapshot(hp: 20, block: 0);
        PlayerSnapshot beforeLethalHit = PlayerSnapshot(hp: 75, block: 5, energy: 3);
        PlayerSnapshot afterLethalHit = PlayerSnapshot(
            hp: 0,
            block: 0,
            energy: 3,
            powers: [new PowerSnapshot("POWER.WEAK_POWER", 1)]);
        PlayerSnapshot afterInterveningHeal = PlayerSnapshot(
            hp: 23,
            block: 0,
            energy: 3,
            powers: [new PowerSnapshot("POWER.WEAK_POWER", 1)]);
        PlayerSnapshot afterFollowUpHit = PlayerSnapshot(
            hp: 20,
            block: 0,
            energy: 3,
            powers: [new PowerSnapshot("POWER.WEAK_POWER", 3)]);
        recorder.RecordEnemyAction(
            enemy,
            "SEGMENTED_MOVE",
            [
                new ActionSnapshotSegment(
                    beforeLethalHit,
                    afterLethalHit,
                    [enemySnapshot],
                    [enemySnapshot]),
                new ActionSnapshotSegment(
                    afterInterveningHeal,
                    afterFollowUpHit,
                    [enemySnapshot],
                    [enemySnapshot]),
            ]);
        recorder.EndCombat(
            victory: false,
            afterFollowUpHit,
            [enemySnapshot],
            new CombatRewards(0, [], null, null, null));
        recorder.ExitFloor();
        recorder.EndRun(won: false, floorsVisited: 1, finalHp: player.Creature.CurrentHp);

        EnemyAction action = Assert.IsType<EnemyAction>(
            Assert.Single(Assert.Single(recorder.BuildCombatLog(combatId).Turns).Actions));
        Assert.Equal(new DamageTakenRecord(83, 5), Assert.Single(action.DamageToTargets));
        Assert.Equal(
            new PowerApplicationRecord("player", "POWER.WEAK_POWER", 3),
            Assert.Single(action.PowersApplied));
    }

    [Fact]
    public void RecordEnemyAction_RejectsEmptySegments()
    {
        var recorder = new RunRecorder();
        (RunState runState, _, CombatState combatState, Creature enemy) =
            CreateCombat("empty-enemy-action");
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        recorder.BeginCombat(runState, RoomType.Monster, "TrainingDummy", combatState);
        recorder.RecordTurnStart(combatState);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => recorder.RecordEnemyAction(enemy, "EMPTY_MOVE", []));

        Assert.Equal("segments", exception.ParamName);
    }

    [Fact]
    public void OutputBeforeEndRun_ThrowsClearInvalidOperationException()
    {
        var recorder = new RunRecorder();

        InvalidOperationException manifestError =
            Assert.Throws<InvalidOperationException>(recorder.BuildManifest);
        InvalidOperationException logsError =
            Assert.Throws<InvalidOperationException>(() => _ = recorder.CombatLogs);
        InvalidOperationException combatError =
            Assert.Throws<InvalidOperationException>(() => recorder.BuildCombatLog("combat-001"));

        Assert.Contains("run has ended", manifestError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("run has ended", logsError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("run has ended", combatError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LifecycleMethods_RejectOutOfOrderCallsWithStateSpecificErrors()
    {
        (RunState runState, _, CombatState combatState, _) = CreateCombat("lifecycle-seed");
        var point = new MapPoint(col: 0, row: 0) { PointType = MapPointType.Monster };

        var noRun = new RunRecorder();
        InvalidOperationException floorError = Assert.Throws<InvalidOperationException>(
            () => noRun.EnterFloor(point, RoomType.Monster));
        Assert.Contains("active run", floorError.Message, StringComparison.OrdinalIgnoreCase);

        var noFloor = new RunRecorder();
        noFloor.BeginRun(runState);
        InvalidOperationException combatError = Assert.Throws<InvalidOperationException>(
            () => noFloor.BeginCombat(runState, RoomType.Monster, "TrainingDummy", combatState));
        Assert.Contains("active floor", combatError.Message, StringComparison.OrdinalIgnoreCase);

        var noTurn = new RunRecorder();
        noTurn.BeginRun(runState);
        noTurn.EnterFloor(point, RoomType.Monster);
        noTurn.BeginCombat(runState, RoomType.Monster, "TrainingDummy", combatState);
        InvalidOperationException drawError = Assert.Throws<InvalidOperationException>(
            () => noTurn.RecordDraw(ModelDb.Card<StrikeRegent>()));
        Assert.Contains("active turn", drawError.Message, StringComparison.OrdinalIgnoreCase);

        InvalidOperationException exitError =
            Assert.Throws<InvalidOperationException>(noTurn.ExitFloor);
        Assert.Contains("combat", exitError.Message, StringComparison.OrdinalIgnoreCase);

        InvalidOperationException endRunError = Assert.Throws<InvalidOperationException>(
            () => noTurn.EndRun(won: false, floorsVisited: 0, finalHp: 75));
        Assert.Contains("floor", endRunError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SameSeedAndLifecycle_ProduceStableFilenameSafeIds()
    {
        const string seed = "unsafe:/\\seed?*";

        string firstRunId = CompleteEmptyRun(seed).RunId;
        string secondRunId = CompleteEmptyRun(seed).RunId;

        Assert.Equal(firstRunId, secondRunId);
        Assert.Matches("^[A-Za-z0-9_-]+$", firstRunId);

        var recorder = new RunRecorder();
        (RunState runState, _, CombatState combatState, _) = CreateCombat(seed);
        recorder.BeginRun(runState);
        recorder.EnterFloor(new MapPoint(0, 0) { PointType = MapPointType.Monster }, RoomType.Monster);

        Assert.Equal("combat-001", recorder.BeginCombat(runState, RoomType.Monster, "TrainingDummy", combatState));
    }

    [Fact]
    public void UnslottedDuplicateEnemies_UseDistinctStableCombatIdsInSnapshotsAndActions()
    {
        RunState runState = CreateRun("duplicate-unslotted-enemies");
        Player player = runState.Players.Single();
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        player.ResetCombatState();
        player.PopulateCombatState(runState.Rng.Shuffle);
        Creature first = combatState.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);
        Creature second = combatState.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);

        var recorder = new RunRecorder();
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        string combatId = recorder.BeginCombat(
            runState,
            RoomType.Monster,
            "duplicate training dummies",
            combatState);
        recorder.RecordTurnStart(combatState);

        PlayerSnapshot playerSnapshot = SnapshotFactory.SnapshotPlayer(player);
        EnemySnapshot[] before = combatState.Enemies.Select(SnapshotFactory.SnapshotEnemy).ToArray();
        EnemySnapshot[] after = [before[0], before[1] with { Hp = before[1].Hp - 6 }];
        CardModel strike = player.Deck.Cards.OfType<StrikeRegent>().First();
        recorder.RecordCardPlay(
            new CardPlay
            {
                Card = strike,
                Player = player,
                Target = second,
                ResultPile = PileType.Discard,
                Resources = new ResourceInfo(1, 1, 0, 0),
                IsAutoPlay = false,
                PlayIndex = 0,
                PlayCount = 1,
            },
            playerSnapshot,
            playerSnapshot,
            before,
            after);
        recorder.RecordEnemyAction(
            first,
            "WAIT",
            [new ActionSnapshotSegment(
                playerSnapshot,
                playerSnapshot,
                after,
                after)]);
        recorder.EndCombat(
            victory: true,
            playerSnapshot,
            after,
            new CombatRewards(0, [], null, null, null));
        recorder.ExitFloor();
        recorder.EndRun(won: true, floorsVisited: 1, finalHp: player.Creature.CurrentHp);

        CombatLog log = recorder.BuildCombatLog(combatId);
        Assert.Equal(["combat:1", "combat:2"], log.EnemiesInitial.Select(enemy => enemy.Slot));
        PlayCardAction cardAction = Assert.IsType<PlayCardAction>(log.Turns[0].Actions[0]);
        Assert.Equal("combat:2", cardAction.Target);
        Assert.Equal("combat:2", Assert.Single(cardAction.DamageDealt).Target);
        EnemyAction enemyAction = Assert.IsType<EnemyAction>(log.Turns[0].Actions[1]);
        Assert.Equal("combat:1", enemyAction.Source);
    }

    [Fact]
    public void EndCombat_ClosesWinningTurnWithoutAddingAnEndTurnAction()
    {
        var recorder = new RunRecorder();
        (RunState runState, _, CombatState combatState, _) = CreateCombat("winning-card");
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        string combatId = recorder.BeginCombat(
            runState,
            RoomType.Monster,
            "TrainingDummy",
            combatState);
        recorder.RecordTurnStart(combatState);

        PlayerSnapshot finalPlayer = SnapshotFactory.SnapshotPlayer(runState.Players.Single());
        EnemySnapshot[] finalEnemies = combatState.Enemies.Select(SnapshotFactory.SnapshotEnemy).ToArray();
        recorder.EndCombat(
            victory: true,
            finalPlayer,
            finalEnemies,
            new CombatRewards(0, [], null, null, null));
        recorder.ExitFloor();
        recorder.EndRun(won: true, floorsVisited: 1, finalHp: 75);

        TurnRecord turn = Assert.Single(recorder.BuildCombatLog(combatId).Turns);
        Assert.DoesNotContain(turn.Actions, action => action is EndTurnAction);
    }

    [Fact]
    public void EndCombat_UpdatesFinalSnapshotsWhenTheLastTurnWasAlreadyEnded()
    {
        var recorder = new RunRecorder();
        (RunState runState, _, CombatState combatState, _) = CreateCombat("ended-turn-final-snapshot");
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        string combatId = recorder.BeginCombat(
            runState,
            RoomType.Monster,
            "TrainingDummy",
            combatState);
        recorder.RecordTurnStart(combatState);
        recorder.RecordEndTurn(
            PlayerSnapshot(hp: 61, block: 0, energy: 0),
            [EnemySnapshot(hp: 20, block: 0)]);

        recorder.EndCombat(
            victory: false,
            PlayerSnapshot(hp: 0, block: 0, energy: 0),
            [EnemySnapshot(hp: 17, block: 0)],
            new CombatRewards(0, [], null, null, null));
        recorder.ExitFloor();
        recorder.EndRun(won: false, floorsVisited: 1, finalHp: 0);

        TurnRecord turn = Assert.Single(recorder.BuildCombatLog(combatId).Turns);
        Assert.Equal(0, turn.PlayerPost.Hp);
        Assert.Equal(17, Assert.Single(turn.EnemiesPost).Hp);
        Assert.IsType<EndTurnAction>(turn.Actions[^1]);
    }

    [Fact]
    public void RecordEnemyAction_ReportsPowersAppliedToANewlySummonedEnemy()
    {
        var recorder = new RunRecorder();
        (RunState runState, _, CombatState combatState, Creature source) = CreateCombat("summoned-power-diff");
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        string combatId = recorder.BeginCombat(
            runState,
            RoomType.Monster,
            "Fogmog",
            combatState);
        recorder.RecordTurnStart(combatState);
        PlayerSnapshot player = PlayerSnapshot(hp: 75, block: 0, energy: 0);
        EnemySnapshot original = EnemySnapshot(hp: 20, block: 0);
        var summoned = new EnemySnapshot(
            Slot: "illusion",
            Id: "MONSTER.EYE_WITH_TEETH",
            Hp: 18,
            MaxHp: 18,
            Block: 0,
            Powers:
            [
                new PowerSnapshot("POWER.ILLUSION_POWER", 1),
                new PowerSnapshot("POWER.MINION_POWER", 1),
            ],
            Intent: "Attack 6");
        recorder.RecordEnemyAction(
            source,
            "ILLUSION_MOVE",
            [new ActionSnapshotSegment(player, player, [original], [original, summoned])]);
        recorder.EndCombat(
            victory: false,
            player,
            [original, summoned],
            new CombatRewards(0, [], null, null, null));
        recorder.ExitFloor();
        recorder.EndRun(won: false, floorsVisited: 1, finalHp: 75);

        EnemyAction action = Assert.Single(recorder.BuildCombatLog(combatId).Turns
            .SelectMany(turn => turn.Actions)
            .OfType<EnemyAction>());
        Assert.Contains(
            action.PowersApplied,
            power => power.Target == "illusion" &&
                     power.Power == "POWER.ILLUSION_POWER" &&
                     power.Amount == 1);
        Assert.Contains(
            action.PowersApplied,
            power => power.Target == "illusion" &&
                     power.Power == "POWER.MINION_POWER" &&
                     power.Amount == 1);
    }

    private static RunManifest CompleteEmptyRun(string seed)
    {
        var recorder = new RunRecorder();
        RunState runState = CreateRun(seed);
        recorder.BeginRun(runState);
        recorder.EndRun(won: false, floorsVisited: 0, finalHp: 75);
        return recorder.BuildManifest();
    }

    private static (RunState RunState, Player Player, CombatState CombatState, Creature Enemy) CreateCombat(string seed)
    {
        RunState runState = CreateRun(seed);
        Player player = runState.Players.Single();
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        player.ResetCombatState();
        player.PopulateCombatState(runState.Rng.Shuffle);
        CardModel drawnCard = player.PlayerCombatState!.DrawPile.Cards.OfType<StrikeRegent>().First();
        player.PlayerCombatState.Hand.AddInternal(drawnCard);
        player.PlayerCombatState!.Energy = 3;

        var monster = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        Creature enemy = combatState.AddMonster(monster, CombatSide.Enemy, "front");
        monster.SetUpForCombat();
        monster.RollMove(combatState.Allies);
        return (runState, player, combatState, enemy);
    }

    private static RunState CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth(), ascensionLevel: 2);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }

    private static PlayerSnapshot PlayerSnapshot(
        int hp,
        int block,
        int energy,
        IReadOnlyList<PowerSnapshot>? powers = null) => new(
            Hp: hp,
            MaxHp: 75,
            Block: block,
            Energy: energy,
            Stars: 0,
            Hand: ["CARD.STRIKE_REGENT"],
            DrawPileSize: 9,
            DiscardPileSize: 0,
            ExhaustPileSize: 0,
            Powers: powers ?? []);

    private static void AssertPlayerSnapshot(PlayerSnapshot expected, PlayerSnapshot actual)
    {
        Assert.Equal(expected.Hp, actual.Hp);
        Assert.Equal(expected.MaxHp, actual.MaxHp);
        Assert.Equal(expected.Block, actual.Block);
        Assert.Equal(expected.Energy, actual.Energy);
        Assert.Equal(expected.Stars, actual.Stars);
        Assert.Equal(expected.Hand, actual.Hand);
        Assert.Equal(expected.DrawPileSize, actual.DrawPileSize);
        Assert.Equal(expected.DiscardPileSize, actual.DiscardPileSize);
        Assert.Equal(expected.ExhaustPileSize, actual.ExhaustPileSize);
        Assert.Equal(expected.Powers, actual.Powers);
    }

    private static void AssertEnemySnapshot(EnemySnapshot expected, EnemySnapshot actual)
    {
        Assert.Equal(expected.Slot, actual.Slot);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Hp, actual.Hp);
        Assert.Equal(expected.MaxHp, actual.MaxHp);
        Assert.Equal(expected.Block, actual.Block);
        Assert.Equal(expected.Powers, actual.Powers);
        Assert.Equal(expected.Intent, actual.Intent);
    }

    private static EnemySnapshot EnemySnapshot(int hp, int block) => new(
        Slot: "front",
        Id: "MONSTER.TRAINING_DUMMY",
        Hp: hp,
        MaxHp: 20,
        Block: block,
        Powers: [],
        Intent: "Attack 8");
}
