using Sts2Sim.Core.Content;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs.Transplant;

namespace Sts2Sim.Core.Runs;

public sealed partial class RunState
{
    internal bool HasGeneratedRoomsForTransplant => _generatedRooms is not null;
    internal int GetAscensionLevelForTransplant() =>
        Enumerable.Range(0, 11).Last(level => Ascension.HasLevel((Sts2Sim.Core.Entities.Ascension.AscensionLevel)level));

    internal void RestoreSharedRelicBagForTransplant(RelicBagSnapshot bag) =>
        SharedRelicGrabBag = RelicGrabBag.ImportForTransplant(bag, "SharedRelicBag");

    private (RoomType Type, EncounterDefinition Encounter, bool Injected)? _nextEncounterForTransplant;

    internal RoomProgressSnapshot ExportRoomProgressForTransplant(RoomType targetRoomType)
    {
        EnsureRoomsGenerated();
        IReadOnlyList<string> normal = Enumerable.Range(0, _normalEncountersVisited)
            .Select(i => _normalEncounterSequence[i % _normalEncounterSequence.Count].IdEntry).ToArray();
        IReadOnlyList<string> elite = Enumerable.Range(0, _eliteEncountersVisited)
            .Select(i => _eliteEncounterSequence[i % _eliteEncounterSequence.Count].IdEntry).ToArray();
        IReadOnlyList<string> boss = Enumerable.Range(0, _bossEncountersVisited)
            .Select(i => i > 0 && _secondBossEncounter is not null
                ? _secondBossEncounter.IdEntry : _bossEncounter.IdEntry).ToArray();
        string next = targetRoomType switch
        {
            RoomType.Monster => _normalEncounterSequence[_normalEncountersVisited % _normalEncounterSequence.Count].IdEntry,
            RoomType.Elite => _eliteEncounterSequence[_eliteEncountersVisited % _eliteEncounterSequence.Count].IdEntry,
            RoomType.Boss => _bossEncountersVisited > 0 && _secondBossEncounter is not null
                ? _secondBossEncounter.IdEntry : _bossEncounter.IdEntry,
            _ => throw new ArgumentOutOfRangeException(nameof(targetRoomType)),
        };
        return new RoomProgressSnapshot(
            _eventsVisited, _normalEncountersVisited, _eliteEncountersVisited,
            _bossEncountersVisited,
            _visitedEventIds.Select(id => id.ToString()).Order(StringComparer.Ordinal).ToArray(),
            normal, elite, boss, next)
        {
            NextRoomId = _nextRoomId,
            CurrentMapPointHasShop = _currentMapPointHasShop,
            PreviousMapPointHasShop = PreviousMapPointHasShop,
        };
    }

    internal void RestoreForTransplant(RunTransplantSnapshot snapshot, RoomType targetRoomType)
    {
        EnsureRoomsGenerated();
        if (snapshot.ActIndex < 0 || snapshot.ActIndex >= _acts.Length)
            throw new TransplantMappingException("ActIndex", "Act index is outside the run.");
        while (CurrentActIndex < snapshot.ActIndex) AdvanceToNextAct();
        if (snapshot.CompletedActFloors < 0)
            throw new TransplantMappingException("CompletedActFloors", "Floor count cannot be negative.");
        _completedActFloors = snapshot.CompletedActFloors;
        _visitedMapCoords.Clear();
        if (snapshot.VisitedMapCoords is null)
            throw new TransplantMappingException("VisitedMapCoords", "Missing map history.");
        for (int index = 0; index < snapshot.VisitedMapCoords.Count; index++)
        {
            MapCoordDto? dto = snapshot.VisitedMapCoords[index];
            if (dto is null || !Map.HasPoint(new MapCoord(dto.Col, dto.Row)))
                throw new TransplantMappingException($"VisitedMapCoords[{index}]", "Map coordinate is absent.");
            if (!AddVisitedMapCoord(new MapCoord(dto.Col, dto.Row)))
                throw new TransplantMappingException($"VisitedMapCoords[{index}]", "Duplicate coordinate.");
        }
        if (!snapshot.IsInjected)
        {
            MapCoord targetCoord = new(snapshot.TargetNode!.Col, snapshot.TargetNode.Row);
            MapPoint? target = Map.GetPoint(targetCoord);
            if (target is null || _visitedMapCoords.Contains(targetCoord))
                throw new TransplantMappingException("TargetNode", "Target must be an unvisited map point.");
            target.PointType = targetRoomType switch
            {
                RoomType.Monster => MapPointType.Monster,
                RoomType.Elite => MapPointType.Elite,
                RoomType.Boss => MapPointType.Boss,
                _ => throw new TransplantMappingException("TargetRoomType", "Target is not a combat room."),
            };
        }
        RoomProgressSnapshot progress = snapshot.RoomProgress ??
            throw new TransplantMappingException("RoomProgress", "Missing room progress.");
        if (progress.NextRoomId is null || progress.CurrentMapPointHasShop is null ||
            progress.PreviousMapPointHasShop is null)
            throw new TransplantMappingException("RoomProgress", "Missing room identity or shop history.");
        if (progress.EventsVisited < 0 || progress.NormalEncountersVisited < 0 ||
            progress.EliteEncountersVisited < 0 || progress.BossEncountersVisited < 0 ||
            progress.NextRoomId < 0)
            throw new TransplantMappingException("RoomProgress", "Negative progress counter.");
        _eventsVisited = progress.EventsVisited;
        _normalEncountersVisited = progress.NormalEncountersVisited;
        _eliteEncountersVisited = progress.EliteEncountersVisited;
        _bossEncountersVisited = progress.BossEncountersVisited;
        _nextRoomId = progress.NextRoomId.Value;
        _currentMapPointHasShop = progress.CurrentMapPointHasShop.Value;
        PreviousMapPointHasShop = progress.PreviousMapPointHasShop.Value;
        _visitedEventIds.Clear();
        if (progress.VisitedEventIds is null)
            throw new TransplantMappingException("RoomProgress.VisitedEventIds", "Missing events.");
        foreach (string id in progress.VisitedEventIds)
        {
            try { _visitedEventIds.Add(ModelId.Deserialize(id)); }
            catch (System.Text.Json.JsonException error)
            { throw new TransplantMappingException("RoomProgress.VisitedEventIds", error.Message); }
        }
        ValidateEncounterHistory(progress.VisitedNormalEncounterIds, _normalEncounterSequence,
            _normalEncountersVisited, "RoomProgress.VisitedNormalEncounterIds");
        ValidateEncounterHistory(progress.VisitedEliteEncounterIds, _eliteEncounterSequence,
            _eliteEncountersVisited, "RoomProgress.VisitedEliteEncounterIds");
        if (progress.VisitedBossEncounterIds is null ||
            progress.VisitedBossEncounterIds.Count != _bossEncountersVisited)
            throw new TransplantMappingException("RoomProgress.VisitedBossEncounterIds", "Count mismatch.");
        for (int index = 0; index < _bossEncountersVisited; index++)
        {
            string expected = index > 0 && _secondBossEncounter is not null
                ? _secondBossEncounter.IdEntry : _bossEncounter.IdEntry;
            if (progress.VisitedBossEncounterIds[index] != expected)
                throw new TransplantMappingException($"RoomProgress.VisitedBossEncounterIds[{index}]",
                    "Encounter differs from generated sequence.");
        }
        if (!snapshot.IsInjected)
        {
            string derived = ExportRoomProgressForTransplant(targetRoomType).NextEncounterId!;
            if (progress.NextEncounterId is null)
                throw new TransplantMappingException("RoomProgress.NextEncounterId", "Missing encounter checkpoint.");
            if (progress.NextEncounterId != derived)
                throw new TransplantMappingException("RoomProgress.NextEncounterId",
                    $"transplant|encounter_mismatch: native={progress.NextEncounterId}, sim={derived}");

            EncounterDefinition nextEncounter = targetRoomType switch
            {
                RoomType.Monster => _normalEncounterSequence[_normalEncountersVisited % _normalEncounterSequence.Count],
                RoomType.Elite => _eliteEncounterSequence[_eliteEncountersVisited % _eliteEncounterSequence.Count],
                RoomType.Boss => _bossEncountersVisited > 0 && _secondBossEncounter is not null
                    ? _secondBossEncounter : _bossEncounter,
                _ => throw new TransplantMappingException("TargetRoomType", "Unknown combat room."),
            };
            SetNextEncounterForTransplant(targetRoomType, nextEncounter);
        }

        RunRngLoad(snapshot.RunRng);
        RunOddsLoad(snapshot.RunOdds);
    }

    private static void ValidateEncounterHistory(
        IReadOnlyList<string>? ids, IReadOnlyList<EncounterDefinition> sequence,
        int count, string path)
    {
        if (ids is null || ids.Count != count)
            throw new TransplantMappingException(path, "Count mismatch.");
        for (int index = 0; index < count; index++)
            if (ids[index] != sequence[index % sequence.Count].IdEntry)
                throw new TransplantMappingException($"{path}[{index}]",
                    "Encounter differs from generated sequence.");
    }

    private void RunRngLoad(Sts2Sim.Core.Saves.SerializableRunRngSet rng)
    {
        if (rng is null || rng.Rngs is null)
            throw new TransplantMappingException("RunRng", "Missing run RNG.");
        Rng.LoadFromSerializable(rng);
    }

    private void RunOddsLoad(Sts2Sim.Core.Saves.SerializableRunOddsSet odds)
    {
        if (odds is null)
            throw new TransplantMappingException("RunOdds", "Missing run odds.");
        Odds.UnknownMapPoint.MonsterOdds = odds.UnknownMapPointMonsterOddsValue;
        Odds.UnknownMapPoint.EliteOdds = odds.UnknownMapPointEliteOddsValue;
        Odds.UnknownMapPoint.TreasureOdds = odds.UnknownMapPointTreasureOddsValue;
        Odds.UnknownMapPoint.ShopOdds = odds.UnknownMapPointShopOddsValue;
    }

    internal EncounterDefinition PullNextEncounterForTransplant(RoomType roomType)
    {
        if (_nextEncounterForTransplant is not { } pending) return PullNextEncounter(roomType);
        if (pending.Type != roomType)
            throw new TransplantMappingException("TargetRoomType", "Encounter override room type differs.");
        _nextEncounterForTransplant = null;
        EnsureRoomsGenerated();
        bool sequenceUnavailable = roomType switch
        {
            RoomType.Monster => _normalEncounterSequence.Count == 0,
            RoomType.Elite => _eliteEncounterSequence.Count == 0,
            RoomType.Boss => _bossEncounter is null,
            _ => false,
        };
        if (sequenceUnavailable || pending.Injected)
        {
            if (roomType == RoomType.Monster) _normalEncountersVisited++;
            else if (roomType == RoomType.Elite) _eliteEncountersVisited++;
            else _bossEncountersVisited++;
            return pending.Encounter;
        }
        EncounterDefinition derived = PullNextEncounter(roomType);
        if (derived.IdEntry != pending.Encounter.IdEntry)
            throw new TransplantMappingException("RoomProgress.NextEncounterId",
                $"transplant|encounter_mismatch: override={pending.Encounter.IdEntry}, sim={derived.IdEntry}");
        return derived;
    }
    internal void SetInjectedEncounterForTransplant(RoomType roomType, string encounterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        if (_nextEncounterForTransplant is not null)
            throw new InvalidOperationException("A transplant encounter is already pending.");
        IReadOnlyList<EncounterDefinition> candidates = roomType switch
        {
            RoomType.Monster => Acts[CurrentActIndex].MonsterEncounterCandidates,
            RoomType.Elite => Acts[CurrentActIndex].EliteEncounterCandidates,
            RoomType.Boss => Acts[CurrentActIndex].BossEncounterCandidates,
            _ => throw new TransplantMappingException("TargetRoomType", "Unsupported injected room type."),
        };
        EncounterDefinition[] matches = candidates.Where(candidate => candidate.IdEntry == encounterId).ToArray();
        if (matches.Length != 1)
            throw new TransplantMappingException("EncounterId",
                "Injected encounter is absent or ambiguous in this act and room pool.");
        _nextEncounterForTransplant = (roomType, matches[0], true);
    }
    internal void SetNextEncounterForTransplant(RoomType roomType, EncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        if (_nextEncounterForTransplant is not null)
            throw new InvalidOperationException("A transplant encounter override is already pending.");
        _nextEncounterForTransplant = (roomType, encounter, false);
    }
}
