using System.Collections.ObjectModel;
using System.Text;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Reporting;

public sealed class RunRecorder : IRunRecorder
{
    private const string SchemaVersion = ReportingSchema.CurrentVersion;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly List<FloorEntry> _floors = [];
    private readonly Dictionary<string, CombatLog> _completedCombatLogs = new(StringComparer.Ordinal);
    private RunState? _runState;
    private string? _runId;
    private DateTimeOffset _runStartedAtUtc;
    private bool _runEnded;
    private RunManifest? _manifest;
    private IReadOnlyDictionary<string, CombatLog>? _publishedCombatLogs;
    private FloorBuilder? _currentFloor;
    private CombatBuilder? _currentCombat;
    private TurnBuilder? _currentTurn;
    private int _nextCombatIndex;

    public RunRecorder(Func<DateTimeOffset>? utcNow = null) =>
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    public IReadOnlyDictionary<string, CombatLog> CombatLogs
    {
        get
        {
            EnsureRunEnded();
            return _publishedCombatLogs!;
        }
    }

    public void BeginRun(RunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        if (_runState is not null)
        {
            throw new InvalidOperationException("BeginRun can only be called once for a recorder.");
        }

        _ = GetSinglePlayer(runState, nameof(BeginRun));
        _runState = runState;
        _runId = CreateRunId(runState);
        _runStartedAtUtc = _utcNow();
    }

    public void EnterFloor(MapPoint point, RoomType roomType)
    {
        RunState runState = RequireActiveRun(nameof(EnterFloor));
        ArgumentNullException.ThrowIfNull(point);
        if (_currentFloor is not null)
        {
            throw new InvalidOperationException("EnterFloor cannot be called while another floor is active.");
        }

        Player player = GetSinglePlayer(runState, nameof(EnterFloor));
        _currentFloor = new FloorBuilder
        {
            Index = _floors.Count + 1,
            Coord = new MapCoordinate(point.coord.col, point.coord.row),
            PointType = point.PointType.ToString(),
            RoomType = roomType,
            HpBefore = player.Creature.CurrentHp,
            GoldBefore = player.Gold,
            DeckBefore = SnapshotFactory.SnapshotDeck(player).ToArray(),
            RelicsBefore = SnapshotFactory.SnapshotRelics(player).ToArray(),
            PotionsBefore = SnapshotFactory.SnapshotPotions(player).ToArray(),
        };
    }

    public void RecordFloorDetail(FloorDetail detail)
    {
        _ = RequireActiveRun(nameof(RecordFloorDetail));
        ArgumentNullException.ThrowIfNull(detail);
        FloorBuilder floor = RequireActiveFloor(nameof(RecordFloorDetail));
        if (_currentCombat is not null)
        {
            throw new InvalidOperationException("RecordFloorDetail cannot be called while a combat is active.");
        }

        if (floor.Detail is not null)
        {
            throw new InvalidOperationException("RecordFloorDetail cannot replace an already recorded floor detail.");
        }

        ValidateFloorDetail(floor, detail);
        floor.Detail = Copy(detail);
    }

    public string BeginCombat(
        RunState runState,
        RoomType encounterType,
        string encounterName,
        CombatState combatState)
    {
        RunState activeRun = RequireActiveRun(nameof(BeginCombat));
        FloorBuilder floor = RequireActiveFloor(nameof(BeginCombat));
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterName);
        ArgumentNullException.ThrowIfNull(combatState);
        if (_currentCombat is not null)
        {
            throw new InvalidOperationException("BeginCombat cannot be called while another combat is active.");
        }

        if (!ReferenceEquals(activeRun, runState) || !ReferenceEquals(combatState.RunState, activeRun))
        {
            throw new InvalidOperationException("BeginCombat must use the active run state.");
        }

        if (!encounterType.IsCombatRoom())
        {
            throw new ArgumentOutOfRangeException(
                nameof(encounterType),
                encounterType,
                "A combat encounter must use Monster, Elite, or Boss room type.");
        }

        Player player = GetSinglePlayer(runState, nameof(BeginCombat));
        string combatId = $"combat-{++_nextCombatIndex:D3}";
        PlayerSnapshot snapshot = SnapshotFactory.SnapshotPlayer(player);
        var playerInitial = new CombatPlayerInitialSnapshot(
            snapshot.Hp,
            snapshot.MaxHp,
            snapshot.Block,
            snapshot.Energy,
            player.Deck.Cards.Count,
            SnapshotFactory.SnapshotRelics(player).ToArray(),
            SnapshotFactory.SnapshotPotions(player).ToArray());
        CombatEnemyInitialSnapshot[] enemiesInitial = combatState.Enemies
            .Select(SnapshotFactory.SnapshotEnemy)
            .Select(enemy => new CombatEnemyInitialSnapshot(
                enemy.Slot,
                enemy.Id,
                enemy.Hp,
                enemy.MaxHp,
                enemy.Powers.ToArray()))
            .ToArray();

        _currentCombat = new CombatBuilder
        {
            Id = combatId,
            FloorIndex = floor.Index,
            EncounterType = encounterType,
            EncounterName = encounterName,
            State = combatState,
            StartedAtUtc = _utcNow(),
            PlayerInitial = playerInitial,
            EnemiesInitial = enemiesInitial,
        };
        return combatId;
    }

    public void RecordTurnStart(CombatState combatState)
    {
        _ = RequireActiveRun(nameof(RecordTurnStart));
        ArgumentNullException.ThrowIfNull(combatState);
        CombatBuilder combat = RequireActiveCombat(nameof(RecordTurnStart));
        if (_currentTurn is not null)
        {
            throw new InvalidOperationException("RecordTurnStart cannot be called while another turn is active.");
        }

        if (!ReferenceEquals(combat.State, combatState))
        {
            throw new InvalidOperationException("RecordTurnStart must use the active combat state.");
        }

        PlayerSnapshot playerPre = Copy(SnapshotFactory.SnapshotPlayer(GetSinglePlayer(_runState!, nameof(RecordTurnStart))));
        EnemySnapshot[] enemiesPre = SnapshotEnemies(combatState);
        _currentTurn = new TurnBuilder
        {
            Index = combat.Turns.Count + 1,
            Side = combatState.CurrentSide.ToString().ToLowerInvariant(),
            PlayerPre = playerPre,
            EnemiesPre = enemiesPre,
            PlayerPost = playerPre,
            EnemiesPost = enemiesPre,
        };
    }

    public void RecordDraw(CardModel card)
    {
        _ = RequireActiveRun(nameof(RecordDraw));
        ArgumentNullException.ThrowIfNull(card);
        RequireActiveTurn(nameof(RecordDraw)).Draws.Add(new DrawRecord(card.Id.ToString()));
    }

    public void RecordCardPlay(
        CardPlay cardPlay,
        PlayerSnapshot before,
        PlayerSnapshot after,
        IReadOnlyList<EnemySnapshot> enemiesBefore,
        IReadOnlyList<EnemySnapshot> enemiesAfter)
    {
        _ = RequireActiveRun(nameof(RecordCardPlay));
        ArgumentNullException.ThrowIfNull(cardPlay);
        ArgumentOutOfRangeException.ThrowIfNegative(cardPlay.Resources.EnergySpent);
        ValidateSnapshots(before, after, enemiesBefore, enemiesAfter);
        TurnBuilder turn = RequireActiveTurn(nameof(RecordCardPlay));
        turn.Actions.Add(new PlayCardAction(
            cardPlay.Card.Id.ToString(),
            TargetId(cardPlay.Target),
            cardPlay.Resources.EnergySpent,
            CalculateDamageDealt(enemiesBefore, enemiesAfter),
            CalculatePowerApplications(before, after, enemiesBefore, enemiesAfter),
            Math.Max(0, after.Block - before.Block),
            cardPlay.PlayCount));
        UpdateTurnPost(turn, after, enemiesAfter);
    }

    public void RecordPotionUse(
        PotionModel potion,
        Creature? target,
        PlayerSnapshot before,
        PlayerSnapshot after,
        IReadOnlyList<EnemySnapshot> enemiesBefore,
        IReadOnlyList<EnemySnapshot> enemiesAfter) =>
        RecordPotionUse(
            potion,
            target,
            before,
            after,
            enemiesBefore,
            enemiesAfter,
            consumed: false,
            Array.Empty<string?>(),
            Array.Empty<string?>());

    public void RecordPotionUse(
        PotionModel potion,
        Creature? target,
        PlayerSnapshot before,
        PlayerSnapshot after,
        IReadOnlyList<EnemySnapshot> enemiesBefore,
        IReadOnlyList<EnemySnapshot> enemiesAfter,
        bool consumed,
        IReadOnlyList<string?> potionSlotsBefore,
        IReadOnlyList<string?> potionSlotsAfter)
    {
        _ = RequireActiveRun(nameof(RecordPotionUse));
        ArgumentNullException.ThrowIfNull(potion);
        ArgumentNullException.ThrowIfNull(potionSlotsBefore);
        ArgumentNullException.ThrowIfNull(potionSlotsAfter);
        ValidateSnapshots(before, after, enemiesBefore, enemiesAfter);
        TurnBuilder turn = RequireActiveTurn(nameof(RecordPotionUse));
        turn.Actions.Add(new UsePotionAction(
            potion.Id.ToString(),
            TargetId(target),
            CalculateDamageDealt(enemiesBefore, enemiesAfter),
            CalculatePowerApplications(before, after, enemiesBefore, enemiesAfter))
        {
            HealingReceived = Math.Max(0, after.Hp - before.Hp),
            Consumed = consumed,
            PotionsAdded = CalculatePotionDifference(potionSlotsAfter, potionSlotsBefore),
            PotionsRemoved = CalculatePotionDifference(potionSlotsBefore, potionSlotsAfter),
        });
        UpdateTurnPost(turn, after, enemiesAfter);
    }

    public void RecordEnemyAction(
        Creature source,
        string moveId,
        IReadOnlyList<ActionSnapshotSegment> segments) =>
        RecordEnemyActionCore(source, moveId, segments, resolvedDamage: null);

    public void RecordEnemyAction(
        Creature source,
        string moveId,
        IReadOnlyList<ActionSnapshotSegment> segments,
        IReadOnlyList<DamageResult> resolvedDamage)
    {
        ArgumentNullException.ThrowIfNull(resolvedDamage);
        RecordEnemyActionCore(source, moveId, segments, resolvedDamage);
    }

    private void RecordEnemyActionCore(
        Creature source,
        string moveId,
        IReadOnlyList<ActionSnapshotSegment> segments,
        IReadOnlyList<DamageResult>? resolvedDamage)
    {
        _ = RequireActiveRun(nameof(RecordEnemyAction));
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(moveId);
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0)
        {
            throw new ArgumentException("Enemy actions require at least one snapshot segment.", nameof(segments));
        }

        foreach (ActionSnapshotSegment segment in segments)
        {
            ArgumentNullException.ThrowIfNull(segment);
            ValidateSnapshots(
                segment.PlayerBefore,
                segment.PlayerAfter,
                segment.EnemiesBefore,
                segment.EnemiesAfter);
        }

        TurnBuilder turn = RequireActiveTurn(nameof(RecordEnemyAction));
        DamageTakenRecord[] damage;
        if (resolvedDamage is null)
        {
            int blockAbsorbed = segments.Sum(segment =>
                Math.Max(0, segment.PlayerBefore.Block - segment.PlayerAfter.Block));
            int hpLost = segments.Sum(segment =>
                Math.Max(0, segment.PlayerBefore.Hp - segment.PlayerAfter.Hp));
            damage = hpLost + blockAbsorbed > 0
                ? [new DamageTakenRecord(hpLost + blockAbsorbed, blockAbsorbed)]
                : [];
        }
        else
        {
            damage = resolvedDamage
                .Where(result => result.Receiver.IsPlayer && result.TotalDamage > 0)
                .Select(result => new DamageTakenRecord(result.TotalDamage, result.BlockedDamage))
                .ToArray();
        }

        PowerApplicationRecord[] powers = segments
            .SelectMany(segment => CalculatePowerApplications(
                segment.PlayerBefore,
                segment.PlayerAfter,
                segment.EnemiesBefore,
                segment.EnemiesAfter))
            .GroupBy(application => (application.Target, application.Power))
            .Select(group => new PowerApplicationRecord(
                group.Key.Target,
                group.Key.Power,
                group.Sum(application => application.Amount)))
            .ToArray();
        turn.Actions.Add(new EnemyAction(
            TargetId(source)!,
            moveId,
            damage,
            powers));
        ActionSnapshotSegment finalSegment = segments[^1];
        UpdateTurnPost(turn, finalSegment.PlayerAfter, finalSegment.EnemiesAfter);
    }

    public void RecordEndTurn(
        PlayerSnapshot finalPlayer,
        IReadOnlyList<EnemySnapshot> finalEnemies)
    {
        _ = RequireActiveRun(nameof(RecordEndTurn));
        ArgumentNullException.ThrowIfNull(finalPlayer);
        ArgumentNullException.ThrowIfNull(finalEnemies);
        TurnBuilder turn = RequireActiveTurn(nameof(RecordEndTurn));
        UpdateTurnPost(turn, finalPlayer, finalEnemies);
        turn.Actions.Add(new EndTurnAction());
        CompleteCurrentTurn();
    }

    public void EndCombat(
        bool victory,
        PlayerSnapshot finalPlayer,
        IReadOnlyList<EnemySnapshot> finalEnemies,
        CombatRewards rewards)
    {
        _ = RequireActiveRun(nameof(EndCombat));
        ArgumentNullException.ThrowIfNull(finalPlayer);
        ArgumentNullException.ThrowIfNull(finalEnemies);
        ArgumentNullException.ThrowIfNull(rewards);
        FloorBuilder floor = RequireActiveFloor(nameof(EndCombat));
        CombatBuilder combat = RequireActiveCombat(nameof(EndCombat));
        if (floor.RoomType.IsCombatRoom() && floor.Detail is not null)
        {
            throw new InvalidOperationException("EndCombat cannot replace an already recorded combat floor detail.");
        }

        if (_currentTurn is not null)
        {
            UpdateTurnPost(_currentTurn, finalPlayer, finalEnemies);
            CompleteCurrentTurn();
        }
        else if (combat.Turns.Count > 0)
        {
            int lastIndex = combat.Turns.Count - 1;
            TurnRecord lastTurn = combat.Turns[lastIndex];
            combat.Turns[lastIndex] = lastTurn with
            {
                PlayerPost = Copy(finalPlayer),
                EnemiesPost = finalEnemies.Select(Copy).ToArray(),
            };
        }

        var log = new CombatLog(
            SchemaVersion,
            _runId!,
            combat.Id,
            combat.FloorIndex,
            combat.EncounterType.ToString(),
            combat.EncounterName,
            combat.StartedAtUtc,
            _utcNow(),
            victory ? "victory" : "defeat",
            combat.PlayerInitial,
            combat.EnemiesInitial,
            combat.Turns.ToArray(),
            Copy(rewards));
        _completedCombatLogs.Add(combat.Id, log);
        if (floor.RoomType.IsCombatRoom())
        {
            floor.Detail = new CombatFloorDetail(
                combat.Id,
                $"combats/{floor.Index:D3}_{combat.Id}.json",
                victory);
        }

        _currentCombat = null;
    }

    public void ExitFloor()
    {
        RunState runState = RequireActiveRun(nameof(ExitFloor));
        FloorBuilder floor = RequireActiveFloor(nameof(ExitFloor));
        if (_currentCombat is not null)
        {
            throw new InvalidOperationException("ExitFloor cannot be called while a combat is active.");
        }

        if (floor.Detail is null)
        {
            throw new InvalidOperationException("ExitFloor requires recorded floor detail.");
        }

        Player player = GetSinglePlayer(runState, nameof(ExitFloor));
        _floors.Add(new FloorEntry(
            floor.Index,
            floor.Coord,
            floor.PointType,
            floor.RoomType.ToString(),
            floor.HpBefore,
            player.Creature.CurrentHp,
            player.Creature.MaxHp,
            floor.GoldBefore,
            player.Gold,
            floor.DeckBefore,
            SnapshotFactory.SnapshotDeck(player).ToArray(),
            floor.RelicsBefore,
            SnapshotFactory.SnapshotRelics(player).ToArray(),
            floor.PotionsBefore,
            SnapshotFactory.SnapshotPotions(player).ToArray(),
            floor.Detail));
        _currentFloor = null;
    }

    public void EndRun(
        bool won,
        int floorsVisited,
        int finalHp,
        bool reachedBoss = false,
        bool? survived = null,
        bool truncated = false,
        int actsCleared = 0)
    {
        RunState runState = RequireActiveRun(nameof(EndRun));
        if (_currentFloor is not null)
        {
            throw new InvalidOperationException("EndRun cannot be called while a floor is active.");
        }

        if (floorsVisited != _floors.Count)
        {
            throw new ArgumentException(
                $"EndRun reported {floorsVisited} floors visited, but {_floors.Count} floors were recorded.",
                nameof(floorsVisited));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(finalHp);
        Player player = GetSinglePlayer(runState, nameof(EndRun));
        _manifest = new RunManifest(
            SchemaVersion,
            _runId!,
            runState.Rng.StringSeed,
            player.Character.Id.Entry,
            GetAscensionLevel(runState.Ascension),
            _runStartedAtUtc,
            _utcNow(),
            won ? "victory" : "defeat",
            finalHp,
            player.Creature.MaxHp,
            floorsVisited,
            _floors.ToArray())
        {
            CombatLogs = _completedCombatLogs.Values
                .OrderBy(log => log.Floor)
                .ThenBy(log => log.CombatId, StringComparer.Ordinal)
                .Select(log => new CombatLogRef(
                    log.CombatId,
                    log.Floor,
                    $"combats/{log.Floor:D3}_{log.CombatId}.json"))
                .ToArray(),
            ReachedBoss = reachedBoss,
            Survived = survived ?? finalHp > 0,
            Truncated = truncated,
            ActsCleared = actsCleared,
        };
        _publishedCombatLogs = new ReadOnlyDictionary<string, CombatLog>(
            new Dictionary<string, CombatLog>(_completedCombatLogs, StringComparer.Ordinal));
        _runEnded = true;
    }

    public RunManifest BuildManifest()
    {
        EnsureRunEnded();
        return _manifest!;
    }

    public CombatLog BuildCombatLog(string combatId)
    {
        EnsureRunEnded();
        ArgumentException.ThrowIfNullOrWhiteSpace(combatId);
        return CombatLogs.TryGetValue(combatId, out CombatLog? log)
            ? log
            : throw new KeyNotFoundException($"No combat log exists for combat ID '{combatId}'.");
    }

    private RunState RequireActiveRun(string operation)
    {
        if (_runState is null || _runEnded)
        {
            throw new InvalidOperationException($"{operation} requires an active run.");
        }

        return _runState;
    }

    private FloorBuilder RequireActiveFloor(string operation) =>
        _currentFloor ?? throw new InvalidOperationException($"{operation} requires an active floor.");

    private CombatBuilder RequireActiveCombat(string operation) =>
        _currentCombat ?? throw new InvalidOperationException($"{operation} requires an active combat.");

    private TurnBuilder RequireActiveTurn(string operation) =>
        _currentTurn ?? throw new InvalidOperationException($"{operation} requires an active turn.");

    private void EnsureRunEnded()
    {
        if (!_runEnded)
        {
            throw new InvalidOperationException("Report output is available only after the run has ended.");
        }
    }

    private void CompleteCurrentTurn()
    {
        CombatBuilder combat = RequireActiveCombat(nameof(CompleteCurrentTurn));
        TurnBuilder turn = RequireActiveTurn(nameof(CompleteCurrentTurn));
        combat.Turns.Add(new TurnRecord(
            turn.Index,
            turn.Side,
            turn.PlayerPre,
            turn.EnemiesPre,
            turn.Draws.ToArray(),
            turn.Actions.ToArray(),
            turn.PlayerPost,
            turn.EnemiesPost));
        _currentTurn = null;
    }

    private static void UpdateTurnPost(
        TurnBuilder turn,
        PlayerSnapshot playerAfter,
        IReadOnlyList<EnemySnapshot> enemiesAfter)
    {
        turn.PlayerPost = Copy(playerAfter);
        turn.EnemiesPost = enemiesAfter.Select(Copy).ToArray();
    }

    private static void ValidateSnapshots(
        PlayerSnapshot before,
        PlayerSnapshot after,
        IReadOnlyList<EnemySnapshot> enemiesBefore,
        IReadOnlyList<EnemySnapshot> enemiesAfter)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(enemiesBefore);
        ArgumentNullException.ThrowIfNull(enemiesAfter);
    }

    private static DamageDealtRecord[] CalculateDamageDealt(
        IReadOnlyList<EnemySnapshot> before,
        IReadOnlyList<EnemySnapshot> after)
    {
        var result = new List<DamageDealtRecord>();
        foreach ((EnemySnapshot beforeEnemy, EnemySnapshot afterEnemy) in CorrelateEnemies(before, after))
        {
            int amount = Math.Max(0, beforeEnemy.Hp - afterEnemy.Hp) +
                         Math.Max(0, beforeEnemy.Block - afterEnemy.Block);
            if (amount > 0)
            {
                result.Add(new DamageDealtRecord(EnemyTargetId(beforeEnemy), amount));
            }
        }

        return result.ToArray();
    }

    private static PowerApplicationRecord[] CalculatePowerApplications(
        PlayerSnapshot playerBefore,
        PlayerSnapshot playerAfter,
        IReadOnlyList<EnemySnapshot> enemiesBefore,
        IReadOnlyList<EnemySnapshot> enemiesAfter)
    {
        var result = new List<PowerApplicationRecord>();
        AddPowerApplications(result, "player", playerBefore.Powers, playerAfter.Powers);
        Dictionary<string, EnemySnapshot> beforeByKey = KeyEnemies(enemiesBefore)
            .ToDictionary(pair => pair.Key, pair => pair.Enemy, StringComparer.Ordinal);
        foreach ((string key, EnemySnapshot afterEnemy) in KeyEnemies(enemiesAfter))
        {
            IReadOnlyList<PowerSnapshot> beforePowers = beforeByKey.TryGetValue(
                key,
                out EnemySnapshot? beforeEnemy)
                ? beforeEnemy.Powers
                : Array.Empty<PowerSnapshot>();
            AddPowerApplications(
                result,
                EnemyTargetId(afterEnemy),
                beforePowers,
                afterEnemy.Powers);
        }

        return result.ToArray();
    }

    private static void AddPowerApplications(
        List<PowerApplicationRecord> result,
        string target,
        IReadOnlyList<PowerSnapshot> before,
        IReadOnlyList<PowerSnapshot> after)
    {
        Dictionary<string, int> beforeAmounts = before
            .GroupBy(power => power.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(power => power.Amount), StringComparer.Ordinal);
        foreach (IGrouping<string, PowerSnapshot> group in after.GroupBy(power => power.Id, StringComparer.Ordinal))
        {
            int delta = group.Sum(power => power.Amount) - beforeAmounts.GetValueOrDefault(group.Key);
            if (delta > 0)
            {
                result.Add(new PowerApplicationRecord(target, group.Key, delta));
            }
        }
    }

    private static PlayerSnapshot Copy(PlayerSnapshot snapshot) => new(
        snapshot.Hp,
        snapshot.MaxHp,
        snapshot.Block,
        snapshot.Energy,
        snapshot.Stars,
        snapshot.Hand.ToArray(),
        snapshot.DrawPileSize,
        snapshot.DiscardPileSize,
        snapshot.ExhaustPileSize,
        snapshot.Powers.ToArray());

    private static EnemySnapshot Copy(EnemySnapshot snapshot) => new(
        snapshot.Slot,
        snapshot.Id,
        snapshot.Hp,
        snapshot.MaxHp,
        snapshot.Block,
        snapshot.Powers.ToArray(),
        snapshot.Intent);

    private static FloorDetail Copy(FloorDetail detail) => detail switch
    {
        CombatFloorDetail combat => new CombatFloorDetail(combat.CombatId, combat.CombatLogFile, combat.Victory),
        RestSiteFloorDetail rest => new RestSiteFloorDetail(rest.Decision, rest.HealAmount, rest.UpgradedCard),
        ShopFloorDetail shop => new ShopFloorDetail(
            shop.Purchases.Select(purchase => new PurchaseRecord(
                purchase.Kind,
                purchase.Id,
                purchase.Price)).ToArray()),
        TreasureFloorDetail treasure => new TreasureFloorDetail(treasure.GoldGained, treasure.RelicGained),
        EventFloorDetail @event => new EventFloorDetail(
            @event.EventName,
            @event.OptionChosen,
            @event.EffectsSummary),
        AncientFloorDetail ancient => new AncientFloorDetail(ancient.EventName, ancient.OptionChosen),
        _ => throw new ArgumentOutOfRangeException(nameof(detail), detail, "Unsupported floor detail type."),
    };

    private static string[] CalculatePotionDifference(
        IReadOnlyList<string?> source,
        IReadOnlyList<string?> subtract)
    {
        var remaining = subtract.OfType<string>().ToList();
        var result = new List<string>();
        foreach (string potion in source.OfType<string>())
        {
            int match = remaining.FindIndex(candidate =>
                StringComparer.Ordinal.Equals(candidate, potion));
            if (match >= 0)
            {
                remaining.RemoveAt(match);
            }
            else
            {
                result.Add(potion);
            }
        }

        return result.ToArray();
    }

    private static CombatRewards Copy(CombatRewards rewards) => new(
        rewards.Gold,
        rewards.CardsOffered.ToArray(),
        rewards.CardTaken,
        rewards.RelicTaken,
        rewards.PotionTaken)
    {
        CardsTaken = rewards.CardsTaken.ToArray(),
        RelicsTaken = rewards.RelicsTaken.ToArray(),
        PotionsTaken = rewards.PotionsTaken.ToArray(),
    };

    private static void ValidateFloorDetail(FloorBuilder floor, FloorDetail detail)
    {
        if (floor.RoomType.IsCombatRoom())
        {
            throw new InvalidOperationException("Combat floor detail is recorded by EndCombat.");
        }

        bool matches = (floor.RoomType, detail) switch
        {
            (RoomType.RestSite, RestSiteFloorDetail) => true,
            (RoomType.Shop, ShopFloorDetail) => true,
            (RoomType.Treasure, TreasureFloorDetail) => true,
            (RoomType.Event, AncientFloorDetail) =>
                floor.PointType == MapPointType.Ancient.ToString(),
            (RoomType.Event, EventFloorDetail) =>
                floor.PointType != MapPointType.Ancient.ToString(),
            _ => false,
        };
        if (!matches)
        {
            throw new InvalidOperationException(
                $"{detail.GetType().Name} does not match {floor.RoomType} floor type '{floor.PointType}'.");
        }
    }

    private static IEnumerable<(EnemySnapshot Before, EnemySnapshot After)> CorrelateEnemies(
        IReadOnlyList<EnemySnapshot> before,
        IReadOnlyList<EnemySnapshot> after)
    {
        Dictionary<string, EnemySnapshot> afterByKey = KeyEnemies(after)
            .ToDictionary(pair => pair.Key, pair => pair.Enemy, StringComparer.Ordinal);
        foreach ((string key, EnemySnapshot enemy) in KeyEnemies(before))
        {
            if (afterByKey.TryGetValue(key, out EnemySnapshot? afterEnemy))
            {
                yield return (enemy, afterEnemy);
            }
        }
    }

    private static IEnumerable<(string Key, EnemySnapshot Enemy)> KeyEnemies(
        IReadOnlyList<EnemySnapshot> enemies)
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (EnemySnapshot enemy in enemies)
        {
            string identity = string.IsNullOrWhiteSpace(enemy.Slot)
                ? $"id:{enemy.Id}"
                : $"slot:{enemy.Slot}";
            int occurrence = occurrences.GetValueOrDefault(identity);
            occurrences[identity] = occurrence + 1;
            yield return ($"{identity}#{occurrence}", enemy);
        }
    }

    private static EnemySnapshot[] SnapshotEnemies(CombatState state) =>
        state.Enemies.Select(SnapshotFactory.SnapshotEnemy).Select(Copy).ToArray();

    private static string? TargetId(Creature? creature)
    {
        if (creature is null)
        {
            return null;
        }

        return SnapshotFactory.CombatSlot(creature)
            ?? creature.Monster?.Id.ToString()
            ?? "player";
    }

    private static string EnemyTargetId(EnemySnapshot snapshot) =>
        string.IsNullOrWhiteSpace(snapshot.Slot) ? snapshot.Id : snapshot.Slot;

    private static Player GetSinglePlayer(RunState runState, string operation)
    {
        if (runState.Players.Count != 1)
        {
            throw new InvalidOperationException(
                $"{operation} requires exactly one player because the report schema is single-player.");
        }

        return runState.Players[0];
    }

    private static int GetAscensionLevel(AscensionManager ascension)
    {
        int result = 0;
        foreach (AscensionLevel level in Enum.GetValues<AscensionLevel>())
        {
            if (ascension.HasLevel(level))
            {
                result = Math.Max(result, (int)level);
            }
        }

        return result;
    }

    private static string CreateRunId(RunState runState)
    {
        var slug = new StringBuilder();
        bool previousWasSeparator = false;
        foreach (char character in runState.Rng.StringSeed)
        {
            bool isSafe = char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
            if (isSafe)
            {
                slug.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && slug.Length > 0)
            {
                slug.Append('-');
                previousWasSeparator = true;
            }

            if (slug.Length >= 48)
            {
                break;
            }
        }

        string safeSeed = slug.ToString().Trim('-', '_');
        return $"run-{(safeSeed.Length == 0 ? "seed" : safeSeed)}-{runState.Rng.Seed:x16}";
    }

    private sealed class FloorBuilder
    {
        public required int Index { get; init; }
        public required MapCoordinate Coord { get; init; }
        public required string PointType { get; init; }
        public required RoomType RoomType { get; init; }
        public required int HpBefore { get; init; }
        public required int GoldBefore { get; init; }
        public required IReadOnlyList<string> DeckBefore { get; init; }
        public required IReadOnlyList<string?> RelicsBefore { get; init; }
        public required IReadOnlyList<string?> PotionsBefore { get; init; }
        public FloorDetail? Detail { get; set; }
    }

    private sealed class CombatBuilder
    {
        public required string Id { get; init; }
        public required int FloorIndex { get; init; }
        public required RoomType EncounterType { get; init; }
        public required string EncounterName { get; init; }
        public required CombatState State { get; init; }
        public required DateTimeOffset StartedAtUtc { get; init; }
        public required CombatPlayerInitialSnapshot PlayerInitial { get; init; }
        public required IReadOnlyList<CombatEnemyInitialSnapshot> EnemiesInitial { get; init; }
        public List<TurnRecord> Turns { get; } = [];
    }

    private sealed class TurnBuilder
    {
        public required int Index { get; init; }
        public required string Side { get; init; }
        public required PlayerSnapshot PlayerPre { get; init; }
        public required IReadOnlyList<EnemySnapshot> EnemiesPre { get; init; }
        public required PlayerSnapshot PlayerPost { get; set; }
        public required IReadOnlyList<EnemySnapshot> EnemiesPost { get; set; }
        public List<DrawRecord> Draws { get; } = [];
        public List<ActionRecord> Actions { get; } = [];
    }
}
