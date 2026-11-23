using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs.Transplant;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Runs;

public sealed partial class RunState
{
    internal void MarkUnenteredActBoundary()
    {
        if (CurrentActIndex < 1 || _currentRooms.Count != 0 || _activeRewardOffers.Count != 0 ||
            _eventsVisited != 0 || _normalEncountersVisited != 0 || _eliteEncountersVisited != 0 ||
            _bossEncountersVisited != 0 || _nextRoomId != 0 ||
            _visitedMapCoords.Count > 1 || _visitedMapCoords.Any(c => c != Map.StartingMapPoint.coord))
            throw new InvalidOperationException("Legacy state does not prove an unentered act boundary.");
        EntryPhase = RunEntryPhase.InitializedBeforeAncient;
    }
    internal ActEntrySnapshot ExportActEntryState()
    {
        AssertStableCloneBoundary();
        RequireActEntryBoundary();
        Player player = _players[0];
        if (Rng.UsesSemanticKeys || !player.IsActiveForHooks || player.PlayerCombatState is not null ||
            player.Creature.Powers.Count != 0 || player.Creature.CurrentHp != decimal.Truncate(player.Creature.CurrentHp) ||
            player.Creature.MaxHp != decimal.Truncate(player.Creature.MaxHp))
            throw new TransplantMappingException("ActEntry.Player", "Unsupported RNG mode or non-stable player state.");
        if (_generatedRooms is null || SharedRelicGrabBag is null || !_hasVisibleAncientHistory)
            throw new TransplantMappingException("ActEntry.History", "Missing generated rooms, relic bag or visible Ancient history.");
        var inventory = new PlayerSnapshot(checked((int)player.Creature.CurrentHp), checked((int)player.Creature.MaxHp),
            player.Gold, player.CardRemovalsUsed,
            player.Deck.Cards.Select((card, i) => RunTransplantExporter.ExportCardForTransplant(card, $"Player.Deck[{i}]")).ToArray(),
            player.Relics.Select((relic, i) => new RelicSnapshot(relic.Id.ToString(),
                TransplantSavedProperties.Export(relic, $"Player.Relics[{i}]"))).ToArray(),
            player.PotionSlots.Select(potion => potion?.Id.ToString()).ToArray(),
            player.PlayerRng.ToSerializable(), player.Odds.ToSerializable(), player.RelicGrabBag.ExportForTransplant())
        { MaxEnergy = player.MaxEnergy, BaseOrbSlotCount = player.Character.BaseOrbSlotCount };
        foreach (PotionModel potion in player.PotionSlots.OfType<PotionModel>())
            TransplantFieldRegistry.AssertExportableModel(potion, "Player.Potions");
        var grid = Map.GetAllMapPoints().ToHashSet(ReferenceEqualityComparer.Instance);
        var points = new List<MapPoint>();
        var seen = new HashSet<MapPoint>(ReferenceEqualityComparer.Instance);
        void Add(MapPoint point) { if (seen.Add(point)) points.Add(point); }
        foreach (MapPoint point in Map.GetAllMapPoints()) Add(point);
        Add(Map.StartingMapPoint); Add(Map.BossMapPoint);
        if (Map.SecondBossMapPoint is { } second) Add(second);
        foreach (MapPoint point in Map.startMapPoints) Add(point);
        for (int i = 0; i < points.Count; i++)
        {
            foreach (MapPoint point in points[i].Children) Add(point);
            foreach (MapPoint point in points[i].parents) Add(point);
        }
        ActEntryModelRef ModelRef(AbstractModel model)
        {
            for (int i = 0; i < player.Relics.Count; i++)
                if (ReferenceEquals(model, player.Relics[i])) return new("relic", i);
            for (int i = 0; i < player.Deck.Cards.Count; i++)
                if (ReferenceEquals(model, player.Deck.Cards[i])) return new("card", i);
            throw new TransplantMappingException("ActEntry.Map.Quests", "Quest does not refer to an owned card or relic.");
        }
        string mapKind = Map switch
        {
            StandardActMap => nameof(StandardActMap), SpoilsActMap => nameof(SpoilsActMap),
            GoldenPathActMap => nameof(GoldenPathActMap), ActEntryMapState saved => saved.Kind,
            _ => throw new TransplantMappingException("ActEntry.Map", "Unsupported map implementation.")
        };
        var map = new ActEntryMap(mapKind, Map.GetColumnCount(), Map.GetRowCount(), Coord(Map.StartingMapPoint),
            Coord(Map.BossMapPoint), Map.SecondBossMapPoint is { } boss2 ? Coord(boss2) : null,
            Map.startMapPoints.Select(Coord).ToArray(), points.Select(point => new ActEntryMapPoint(
                point.coord.col, point.coord.row, point.PointType.ToString(), point.CanBeModified, grid.Contains(point),
                point.Children.Select(Coord).ToArray(), point.parents.Select(Coord).ToArray(),
                point.Quests.Select(ModelRef).ToArray())).ToArray());
        return new(ActEntrySnapshot.CurrentSchemaVersion, ActEntrySnapshotCodec.CoreHash, Rng.StringSeed,
            player.Character.Id.ToString(), GetAscensionLevelForTransplant(), CurrentActIndex,
            _acts.Select(act => act.GetType().Name).ToArray(), EntryPhase,
            _visitedMapCoords.Select(c => new MapCoordDto(c.col, c.row)).ToArray(), _completedActFloors,
            inventory, player.Creature.Block, player.CanUseOrRemovePotions, player.BaseOrbSlotCount,
            Rng.ToSerializable(), Odds.ToSerializable(), Odds.UnknownMapPoint.PublicBaseRules,
            Odds.UnknownMapPoint.PublicResetBaseRules, SharedRelicGrabBag.ExportForTransplant(), map,
            _generatedRooms.Select(rooms => new ActEntryRooms(rooms.Events.Select(TypeId).ToArray(),
                rooms.NormalEncounters.Select(e => e.IdEntry).ToArray(), rooms.EliteEncounters.Select(e => e.IdEntry).ToArray(),
                rooms.Boss.IdEntry, rooms.SecondBoss?.IdEntry, TypeId(rooms.Ancient))).ToArray(),
            _visitedAncientTypes.ToDictionary(p => p.Key, p => TypeId(p.Value)), _hasVisibleAncientHistory,
            _visitedEventIds.Select(id => id.ToString()).ToArray(), _eventsVisited, _normalEncountersVisited,
            _eliteEncountersVisited, _bossEncountersVisited, _nextRoomId, _currentMapPointHasShop,
            PreviousMapPointHasShop, Progress.WongoPoints, WongoPointsEarned, FreedRepy,
            _completedQuests.Select(id => id.ToString()).ToArray(), IsImaginationClone);
    }

    private void RequireActEntryBoundary()
    {
        if (IsGameOver || EntryPhase != RunEntryPhase.InitializedBeforeAncient || CurrentActIndex is not (1 or 2) ||
            _players.Count != 1 || _currentRooms.Count != 0 || _activeRewardOffers.Count != 0 ||
            ForcedCombatResumeOutcome is not null || _selectingMapEventBeforeHistoryAppend ||
            _visibleMapVisits.Count != 0 || _unknownMapPointEntriesThisAct != 0 ||
            _visitedMapCoords.Any(c => c != Map.StartingMapPoint.coord) || _visitedMapCoords.Count > 1 ||
            _eventsVisited != 0 || _normalEncountersVisited != 0 || _eliteEncountersVisited != 0 ||
            _bossEncountersVisited != 0 || _nextRoomId != 0)
            throw new TransplantMappingException("ActEntry.Phase", "Only initialized act 2 or 3 before Ancient entry is supported.");
    }

    private static MapCoordDto Coord(MapPoint p) => new(p.coord.col, p.coord.row);
    private static string TypeId(Type type) => type.FullName ?? throw new InvalidOperationException("Content type has no full name.");

    internal static RunState ImportActEntryState(ActEntrySnapshot s)
    {
        if (s.Phase != RunEntryPhase.InitializedBeforeAncient || s.ActIndex is not (1 or 2) || s.Ascension is < 0 or > 10 ||
            string.IsNullOrEmpty(s.Seed) || s.RunRng is null || s.RunRng.Seed != s.Seed || s.Acts is null ||
            s.Rooms is null || s.Rooms.Count != s.Acts.Count || s.Player is null || s.Map is null ||
            s.Visited is null || s.AncientHistory is null || !s.CompleteAncientHistory || s.VisitedEvents is null ||
            s.CompletedQuests is null || s.CompletedFloors < 0 || s.Block < 0 || s.BaseOrbSlots < 0)
            throw new TransplantMappingException("ActEntrySnapshot", "Missing or unsupported boundary fields.");
        ActDefinition[] acts = s.Acts.Select(id => id switch
        {
            nameof(Overgrowth) => (ActDefinition)new Overgrowth(), nameof(Underdocks) => new Underdocks(),
            nameof(Hive) => new Hive(), nameof(Glory) => new Glory(),
            _ => throw new TransplantMappingException("ActEntry.Acts", "Unknown act ID.")
        }).ToArray();
        var run = new RunState(s.Seed, acts, s.Ascension, new HeadlessProgress(s.ProgressWongo));
        var player = Player.CreateForNewRun(ModelDb.GetById<CharacterModel>(ModelId.Deserialize(s.CharacterId)), run);
        run.AddPlayer(player);
        player.RestoreForTransplant(s.Player);
        player.BaseOrbSlotCount = s.BaseOrbSlots;
        player.Creature.GainBlockInternal(s.Block);
        player.CanUseOrRemovePotions = s.CanUsePotions;
        run.CurrentActIndex = s.ActIndex;
        Type ContentType(string name) => ContentRegistry.AllTypes.SingleOrDefault(t => t.FullName == name &&
            typeof(EventModel).IsAssignableFrom(t)) ?? throw new TransplantMappingException("ActEntry.Events", "Unknown event type.");
        EncounterDefinition Encounter(int act, string id, RoomType room) => (room switch
        {
            RoomType.Monster => acts[act].MonsterEncounterCandidates,
            RoomType.Elite => acts[act].EliteEncounterCandidates,
            _ => acts[act].BossEncounterCandidates
        }).SingleOrDefault(e => e.IdEntry == id) ?? throw new TransplantMappingException("ActEntry.Encounter", "Unknown encounter ID.");
        run._generatedRooms = s.Rooms.Select((room, i) => new GeneratedActRooms(
            room.Events.Select(ContentType).ToList(), room.Normal.Select(id => Encounter(i, id, RoomType.Monster)).ToList(),
            room.Elite.Select(id => Encounter(i, id, RoomType.Elite)).ToList(), Encounter(i, room.Boss, RoomType.Boss),
            room.SecondBoss is { } second ? Encounter(i, second, RoomType.Boss) : null, ContentType(room.Ancient))).ToArray();
        for (int i = 0; i < acts.Length; i++)
            acts[i].RebuildEncounterBagsFromHistory(run._generatedRooms[i].NormalEncounters, run._generatedRooms[i].EliteEncounters);
        run.InitializeCurrentAct();
        run.Map = new ActEntryMapState(s.Map, player);
        foreach (MapCoordDto coord in s.Visited)
        {
            var value = new MapCoord(coord.Col, coord.Row);
            if (run.Map.GetPoint(value) is null || !run.AddVisitedMapCoord(value))
                throw new TransplantMappingException("ActEntry.Visited", "Invalid or duplicate map visit.");
        }
        run._completedActFloors = s.CompletedFloors;
        run._visitedAncientTypes.Clear();
        foreach (var entry in s.AncientHistory)
        {
            if (entry.Key < 0 || entry.Key >= s.ActIndex)
                throw new TransplantMappingException("ActEntry.AncientHistory", "Ancient history contains an unentered act.");
            run._visitedAncientTypes.Add(entry.Key, ContentType(entry.Value));
        }
        run._hasVisibleAncientHistory = true;
        run._visitedEventIds.Clear();
        foreach (string id in s.VisitedEvents) run._visitedEventIds.Add(ModelId.Deserialize(id));
        run._eventsVisited = s.EventsVisited; run._normalEncountersVisited = s.NormalVisited;
        run._eliteEncountersVisited = s.EliteVisited; run._bossEncountersVisited = s.BossVisited;
        run._nextRoomId = s.NextRoomId; run._currentMapPointHasShop = s.CurrentShop;
        run.PreviousMapPointHasShop = s.PreviousShop;
        run.WongoPointsEarned = s.WongoEarned; run.FreedRepy = s.FreedRepy;
        run._completedQuests.AddRange(s.CompletedQuests.Select(ModelId.Deserialize));
        run.IsImaginationClone = s.Imagination;
        run.RestoreSharedRelicBagForTransplant(s.SharedRelicBag);
        run.Odds.UnknownMapPoint.SetPublicBaseRules(s.ResetBaseRules);
        run.Odds.UnknownMapPoint.ResetToBase();
        run.Odds.UnknownMapPoint.SetPublicBaseRules(s.BaseRules);
        run.RunOddsLoad(s.RunOdds);
        if (s.RunRng.Rngs.Count != Enum.GetValues<Entities.Rngs.RunRngType>().Length ||
            Enum.GetValues<Entities.Rngs.RunRngType>().Any(type => !s.RunRng.Rngs.ContainsKey(type)))
            throw new TransplantMappingException("ActEntry.RunRng", "Missing RNG stream.");
        run.RunRngLoad(s.RunRng);
        run.EntryPhase = s.Phase;
        run.RequireActEntryBoundary();
        return run;
    }

    // Data-only map adapter: it reuses ActMap traversal and clone graph code; it never generates a map.
    internal sealed class ActEntryMapState : ActMap
    {
        internal string Kind { get; }
        public override MapPoint StartingMapPoint { get; }
        public override MapPoint BossMapPoint { get; }
        public override MapPoint? SecondBossMapPoint { get; }
        protected override MapPoint?[,] Grid { get; }
        internal ActEntryMapState(ActEntryMap data, Player player)
        {
            if (data.Kind is not (nameof(StandardActMap) or nameof(SpoilsActMap) or nameof(GoldenPathActMap)) ||
                data.Columns < 1 || data.Rows < 1 || data.Columns > 100 || data.Rows > 100 ||
                data.Points is null || data.Starts is null)
                throw new TransplantMappingException("ActEntry.Map", "Invalid map layout.");
            Kind = data.Kind;
            Grid = new MapPoint?[data.Columns, data.Rows];
            var points = new Dictionary<MapCoord, MapPoint>();
            foreach (ActEntryMapPoint node in data.Points)
            {
                if (!Enum.TryParse(node.Type, out MapPointType type) || !Enum.IsDefined(type))
                    throw new TransplantMappingException("ActEntry.Map.Type", "Unknown map point type.");
                var point = new MapPoint(node.Col, node.Row) { PointType = type, CanBeModified = node.CanBeModified };
                if (!points.TryAdd(point.coord, point)) throw new TransplantMappingException("ActEntry.Map", "Duplicate coordinate.");
                if (node.InGrid)
                {
                    if (node.Col < 0 || node.Col >= data.Columns || node.Row < 0 || node.Row >= data.Rows)
                        throw new TransplantMappingException("ActEntry.Map", "Grid coordinate out of range.");
                    Grid[node.Col, node.Row] = point;
                }
            }
            MapPoint Resolve(MapCoordDto coord) => points.TryGetValue(new(coord.Col, coord.Row), out var point)
                ? point : throw new TransplantMappingException("ActEntry.Map", "Dangling coordinate.");
            StartingMapPoint = Resolve(data.Starting); BossMapPoint = Resolve(data.Boss);
            SecondBossMapPoint = data.SecondBoss is { } boss ? Resolve(boss) : null;
            foreach (var coord in data.Starts) startMapPoints.Add(Resolve(coord));
            foreach (ActEntryMapPoint node in data.Points)
            {
                MapPoint point = points[new(node.Col, node.Row)];
                foreach (var coord in node.Children) if (!point.Children.Add(Resolve(coord)))
                    throw new TransplantMappingException("ActEntry.Map", "Duplicate child.");
                foreach (var coord in node.Parents) if (!point.parents.Add(Resolve(coord)))
                    throw new TransplantMappingException("ActEntry.Map", "Duplicate parent.");
                foreach (var reference in node.Quests)
                    point.AddQuest(reference.Kind switch
                    {
                        "relic" when reference.Index >= 0 && reference.Index < player.Relics.Count => player.Relics[reference.Index],
                        "card" when reference.Index >= 0 && reference.Index < player.Deck.Cards.Count => player.Deck.Cards[reference.Index],
                        _ => throw new TransplantMappingException("ActEntry.Map.Quest", "Invalid owned model reference.")
                    });
            }
            foreach (MapPoint point in points.Values)
                if (point.Children.Any(child => !child.parents.Contains(point)) || point.parents.Any(parent => !parent.Children.Contains(point)))
                    throw new TransplantMappingException("ActEntry.Map", "Asymmetric map edge.");
        }
        private ActEntryMapState(ActEntryMapState source, IReadOnlyDictionary<AbstractModel, AbstractModel> models)
        {
            Kind = source.Kind;
            var graph = source.ClonePointGraph(models);
            Grid = graph.Grid;
            StartingMapPoint = graph.Points[source.StartingMapPoint];
            BossMapPoint = graph.Points[source.BossMapPoint];
            SecondBossMapPoint = source.SecondBossMapPoint is { } boss ? graph.Points[boss] : null;
            foreach (MapPoint point in source.startMapPoints) startMapPoints.Add(graph.Points[point]);
        }
        internal override ActMap CloneForRun(IReadOnlyDictionary<AbstractModel, AbstractModel> models,
            Random.Rng? replacementRng = null) => new ActEntryMapState(this, models);
    }
}
