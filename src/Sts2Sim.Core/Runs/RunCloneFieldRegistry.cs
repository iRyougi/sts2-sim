using System.Reflection;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Odds;

namespace Sts2Sim.Core.Runs;

internal enum RunCloneFieldClass { Cloned, SharedImmutable, Rejected }

/// <summary>Explicit audit of each instance field in the run snapshot graph.</summary>
internal static class RunCloneFieldRegistry
{
    public static IReadOnlyDictionary<string, (RunCloneFieldClass Class, string Reason)> Fields { get; } = Build();

    private static IReadOnlyDictionary<string, (RunCloneFieldClass Class, string Reason)> Build()
    {
        var fields = new Dictionary<string, (RunCloneFieldClass, string)>();
        Register<RunState>(fields,
            cloned: "_players _acts _readOnlyActs _generatedRooms _visitedMapCoords _completedActFloors _selectingMapEventBeforeHistoryAppend _currentMapPointHasShop <PreviousMapPointHasShop>k__BackingField _currentRooms _visibleMapVisits _unknownMapPointEntriesThisAct _activeRewardOffers <ForcedCombatResumeOutcome>k__BackingField _eventSequence _visitedEventIds _visitedAncientTypes _hasVisibleAncientHistory _normalEncounterSequence _eliteEncounterSequence _bossEncounter _secondBossEncounter _bossEncountersVisited _eventsVisited _normalEncountersVisited _eliteEncountersVisited _nextRoomId <Rng>k__BackingField <Odds>k__BackingField <WongoPointsEarned>k__BackingField <FreedRepy>k__BackingField _completedQuests _currentAncientEventType <SharedRelicGrabBag>k__BackingField <Map>k__BackingField <CurrentActIndex>k__BackingField <IsImaginationClone>k__BackingField",
            shared: "<Ascension>k__BackingField <Progress>k__BackingField",
            rejected: "<CardSelectionSource>k__BackingField _nextEncounterForTransplant");
        Register<Player>(fields,
            cloned: "<Creature>k__BackingField <RunState>k__BackingField <IsActiveForHooks>k__BackingField <PlayerCombatState>k__BackingField <Gold>k__BackingField <CanUseOrRemovePotions>k__BackingField <BaseOrbSlotCount>k__BackingField <CardRemovalsUsed>k__BackingField <Deck>k__BackingField _relics _potionSlots <RelicGrabBag>k__BackingField <PlayerRng>k__BackingField <Odds>k__BackingField",
            shared: "<Character>k__BackingField <UnlockState>k__BackingField <MaxEnergy>k__BackingField");
        Register<ActDefinition>(fields,
            cloned: "_weakMonsterEncounterState _regularMonsterEncounterState _eliteEncounterState _lastMonsterEncounter _monsterEncountersPicked");
        Register<ActMap>(fields, cloned: "startMapPoints");
        Register<StandardActMap>(fields,
            cloned: "_pointTypeCounts _mapLength _rng <BossMapPoint>k__BackingField <StartingMapPoint>k__BackingField <SecondBossMapPoint>k__BackingField <Grid>k__BackingField");
        Register<SpoilsActMap>(fields,
            cloned: "_mapLength _rng _treasureRow _pointTypeCounts <BossMapPoint>k__BackingField <StartingMapPoint>k__BackingField <Grid>k__BackingField");
        Register<GoldenPathActMap>(fields,
            cloned: "<BossMapPoint>k__BackingField <StartingMapPoint>k__BackingField <Grid>k__BackingField");
        Register<MapPoint>(fields,
            cloned: "_quests parents coord <CanBeModified>k__BackingField <PointType>k__BackingField <Children>k__BackingField");
        Register<AbstractOdds>(fields, cloned: "_rng <CurrentValue>k__BackingField");
        Register<UnknownMapPointOdds>(fields,
            cloned: "_baseOdds _nonEventOdds <PublicBaseRules>k__BackingField <PublicResetBaseRules>k__BackingField",
            rejected: "_hooks");
        Register<RunOddsSet>(fields, cloned: "<UnknownMapPoint>k__BackingField");
        return fields;
    }

    private static void Register<T>(Dictionary<string, (RunCloneFieldClass, string)> fields,
        string cloned, string shared = "", string rejected = "")
    {
        Type type = typeof(T);
        Add(cloned, RunCloneFieldClass.Cloned, "Independent snapshot or copied value.");
        Add(shared, RunCloneFieldClass.SharedImmutable, "Immutable content or configuration.");
        Add(rejected, RunCloneFieldClass.Rejected, "Runtime decision sources are rebound; pending transplant is a dedicated rejection boundary.");
        void Add(string names, RunCloneFieldClass classification, string reason)
        {
            foreach (string name in names.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (type.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is null)
                    throw new InvalidOperationException($"Clone registry lists a missing field: {type.Name}.{name}");
                fields.Add($"{type.Name}.{name}", (classification, reason));
            }
        }
    }
}
