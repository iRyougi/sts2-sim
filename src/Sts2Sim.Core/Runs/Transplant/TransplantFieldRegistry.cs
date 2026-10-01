using System.Reflection;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Runs.Transplant;

public enum TransplantFieldClass { Snapshot, ResetAtCombatStart, Derived, Ignored }

/// <summary>
/// The combat registry is itself an explicit field manifest. Reuse its concrete-model keys,
/// then add run and player fields. Unknown model fields are deliberately absent and fail audit.
/// </summary>
public static class TransplantFieldRegistry
{
    private static readonly HashSet<string> NativeSavedFields =
    [
        "SeaGlass._characterId",
        "BoneTea._combatsLeft", "BookOfFiveRings._cardsAdded", "DustyTome._ancientCard",
        "EmberTea._combatsLeft", "FakeHappyFlower._turnsSeen", "FakeVenerableTeaSet._isArmed",
        "FurCoat._actIndex", "FurCoat._markedCoordinates", "GalacticDust._starsSpent",
        "Girya._timesLifted", "HappyFlower._turnCounter", "IronClub._cardsPlayed",
        "JossPaper._cardsExhausted", "LastingCandy._combatRewardsSeen", "LizardTail._wasUsed",
        "MawBank._hasItemBeenBought", "Nunchaku._attacksPlayed", "PaelsTooth._removedCards",
        "PaelsWing._rewardsSacrificed", "Pendulum._turnCounter", "PenNib._attacksPlayed",
        "PollinousCore._handsSeen", "SilkenTress._isUsed", "SwordOfStone._eliteVictories",
        "TeaOfDiscourtesy._combatsLeft", "TuningFork._skillsPlayed", "VenerableTeaSet._isArmed",
        "WongosMysteryTicket._combatsFinished", "WongosMysteryTicket._gaveRelics",
        "Dowsing._unknownRoomsEntered", "Guilty._combatsCompleted", "MadScience._type",
        "FishingRod.<CombatsSeen>k__BackingField", "LavaRock.<HasTriggered>k__BackingField",
        "GoldenCompass.<GoldenPathAct>k__BackingField", "ToyBox.<CombatsSeen>k__BackingField",
        "PumpkinCandle.<KindleCount>k__BackingField", "SilverCrucible.<TimesUsed>k__BackingField",
        "SilverCrucible.<TreasureRoomsEntered>k__BackingField",
        "WingedBoots.<TimesUsed>k__BackingField", "SpoilsMap.<SpoilsActIndex>k__BackingField",
        "MadScience.<Rider>k__BackingField",
    ];
    private static readonly HashSet<string> NativeBoundaryResetFields =
    [
        "VelvetChoker._cardsPlayedThisTurn", "MusicBox._wasUsedThisTurn",
        "MusicBox._cardBeingPlayed", "ThrowingAxe._used", "PaelsEye._usedThisCombat",
        "PaelsLegion._cooldown", "PaelsLegion._affectedCardPlay", "PaelsTears._gainNextTurn",
        "ArtOfWar._attackPlayedThisTurn", "ArtOfWar._previousTurnHadNoAttack",
        "BeltBuckle._dexterityApplied", "BurningSticks._wasUsedThisCombat",
        "CentennialPuzzle._triggeredThisCombat", "JossPaper._etherealCount",
        "Kunai._attacksThisTurn", "Kusarigama._attacksThisTurn",
        "LetterOpener._skillsThisTurn", "MiniRegent._triggeredThisTurn",
        "Permafrost._triggeredThisCombat", "Pocketwatch._cardsPlayedThisTurn",
        "Pocketwatch._shouldDrawExtra", "RainbowRing._playedAttack",
        "RainbowRing._playedSkill", "RainbowRing._playedPower",
        "RainbowRing._triggeredThisTurn", "Regalite._usedThisTurn",
        "Shuriken._attacksThisTurn", "UnsettlingLamp._triggeringCard",
        "UnsettlingLamp._finished", "Vambrace._triggeredThisCombat",
        "LavaLamp._tookDamageThisCombat",
        "PaelsEye._eligibleForExtraTurn", "RippleBasin._attackPlayedThisTurn",
        "StoneCalendar._shouldTriggerThisTurn", "Toolbox._wasUsedThisCombat",
        "HistoryCourse._lastAttack", "HistoryCourse._lastAttackTurn",
        "TheHunt._pendingCardRewards", "Abundance._generatedCandidates",
        "Bombardment._hasReplayed",
        "BeatingRemnant._hpLostThisTurn", "BrilliantScarf._cardsPlayed",
        "FakeOrichalcum._shouldTrigger", "Orichalcum._shouldTrigger",
        "OrnamentalFan._attacksThisTurn", "PaelsEye._wasOwnerPartOfLastPlayerTurn",
        "Maul._damage", "Maul._extraDamageFromMaulPlays", "Maul._increase",
        "Momentum._extraDamage",
        "Bolas._playedOnTurnNumber", "ThrummingHatchet._playedOnTurnNumber",
        "TheBall._damage", "Wither._fakeUpgradeLevel",
        "Disintegration.<PowerAmount>k__BackingField",
    ];

    public static IReadOnlyDictionary<string, (TransplantFieldClass Class, string Reason)> Fields => _fields;

    private static IReadOnlyDictionary<string, (TransplantFieldClass Class, string Reason)> Build()
    {
        var result = new Dictionary<string, (TransplantFieldClass, string)>(StringComparer.Ordinal);
        Type[] bases = [typeof(CardModel), typeof(RelicModel), typeof(EnchantmentModel), typeof(PowerModel)];
        foreach (Type type in typeof(CardModel).Assembly.GetTypes()
                     .Where(type => !type.IsAbstract && bases.Any(baseType => baseType.IsAssignableFrom(type))))
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsStatic || !CombatPrivateStateRegistry.TryGetClassification(type, field.Name,
                        out CombatPrivateStateClassification classification)) continue;
                (TransplantFieldClass fieldClass, string reason) = classification switch
                {
                    CombatPrivateStateClassification.Contributor =>
                        (TransplantFieldClass.Snapshot, "Private state affects future behavior; native save mapping required."),
                    CombatPrivateStateClassification.TransientBoundary =>
                        (TransplantFieldClass.ResetAtCombatStart, "Combat-local state resets at the next combat boundary."),
                    CombatPrivateStateClassification.DerivedPublicState =>
                        (TransplantFieldClass.Derived, "Derived from already snapshotted model state."),
                    CombatPrivateStateClassification.IgnoredNoFutureBehavior =>
                        (TransplantFieldClass.Ignored, IgnoredReason(type, field.Name)),
                    _ => throw new InvalidOperationException("Unknown combat-state classification."),
                };
                if (NativeSavedFields.Contains($"{type.Name}.{field.Name}"))
                    (fieldClass, reason) = (TransplantFieldClass.Snapshot,
                        "Native [SavedProperty] persists across combats; requires prop mapping.");
                if (NativeBoundaryResetFields.Contains($"{type.Name}.{field.Name}"))
                    (fieldClass, reason) = (TransplantFieldClass.ResetAtCombatStart,
                        "Native combat-start or combat-end hook resets this field before the next combat.");
                if (type.Name == "SpoilsMap" && field.Name == "<SpoilsCoord>k__BackingField")
                    (fieldClass, reason) = (TransplantFieldClass.Snapshot,
                        "Host exports the unsaved live coordinate explicitly at combat entry.");
                if (type.Name == "Whistle" && field.Name == "_damage")
                    (fieldClass, reason) = (TransplantFieldClass.Derived,
                        "Derived from canonical card vars and upgrade level.");
                result.Add(Key(field), (fieldClass, reason));
            }
        }

        AddRunAndPlayerFields(result);
        return result;
    }

    private static readonly string[] RunSnapshotFields =
    [
        "_players", "_visitedMapCoords", "_completedActFloors", "_currentMapPointHasShop",
        "<PreviousMapPointHasShop>k__BackingField", "_visitedEventIds",
        "_bossEncountersVisited", "_eventsVisited", "_normalEncountersVisited",
        "_eliteEncountersVisited", "_nextRoomId", "<Rng>k__BackingField",
        "<Odds>k__BackingField", "<Progress>k__BackingField",
        "<WongoPointsEarned>k__BackingField", "<FreedRepy>k__BackingField",
        "_completedQuests", "<SharedRelicGrabBag>k__BackingField",
        "<CurrentActIndex>k__BackingField",
    ];

    private static readonly string[] RunDerivedFields =
    [
        "_acts", "_readOnlyActs", "_generatedRooms", "_eventSequence",
        "_normalEncounterSequence", "_eliteEncounterSequence", "_bossEncounter",
        "_secondBossEncounter", "<Ascension>k__BackingField", "_currentAncientEventType",
        "<Map>k__BackingField", "<CardSelectionSource>k__BackingField",
    ];

    private static readonly string[] RunBoundaryFields =
    [
        "_selectingMapEventBeforeHistoryAppend", "_currentRooms", "_nextEncounterForTransplant",
    ];

    private static readonly string[] PlayerSnapshotFields =
    [
        "<Character>k__BackingField", "<Creature>k__BackingField", "<Gold>k__BackingField",
        "<CanUseOrRemovePotions>k__BackingField", "<CardRemovalsUsed>k__BackingField",
        "<Deck>k__BackingField", "_relics", "_potionSlots",
        "<RelicGrabBag>k__BackingField", "<PlayerRng>k__BackingField",
        "<Odds>k__BackingField", "<MaxEnergy>k__BackingField",
    ];

    private static readonly string[] PlayerDerivedFields =
    [
        "<UnlockState>k__BackingField", "<RunState>k__BackingField",
        "<IsActiveForHooks>k__BackingField", "<BaseOrbSlotCount>k__BackingField",
    ];

    private static readonly string[] PlayerBoundaryFields =
    [
        "<PlayerCombatState>k__BackingField",
    ];

    private static readonly IReadOnlyDictionary<string, (TransplantFieldClass Class, string Reason)> _fields = Build();

    private static void AddRunAndPlayerFields(
        Dictionary<string, (TransplantFieldClass Class, string Reason)> result)
    {
        AddDeclaredFields(result, typeof(RunState), RunSnapshotFields, TransplantFieldClass.Snapshot,
            "Preserved at the combat-entry boundary; the importer restores this run state.");
        AddDeclaredFields(result, typeof(RunState), RunDerivedFields, TransplantFieldClass.Derived,
            "Regenerated from the seed and ordered act definitions before RNG state is restored.");
        AddDeclaredFields(result, typeof(RunState), RunBoundaryFields, TransplantFieldClass.ResetAtCombatStart,
            "No active room or pending transition exists at the export boundary.");
        AddDeclaredFields(result, typeof(Player), PlayerSnapshotFields, TransplantFieldClass.Snapshot,
            "Preserved in PlayerSnapshot or explicitly validated before export.");
        AddDeclaredFields(result, typeof(Player), PlayerDerivedFields, TransplantFieldClass.Derived,
            "Recreated with the player from character and run definitions.");
        AddDeclaredFields(result, typeof(Player), PlayerBoundaryFields, TransplantFieldClass.ResetAtCombatStart,
            "Combat state is created by normal combat entry.");
    }

    private static void AddDeclaredFields(
        Dictionary<string, (TransplantFieldClass Class, string Reason)> result,
        Type type, IEnumerable<string> names, TransplantFieldClass classification, string reason)
    {
        foreach (string name in names)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly) ??
                throw new InvalidOperationException($"Transplant field registration is stale: {type.Name}.{name}");
            result.Add(Key(field), (classification, reason));
        }
    }
    internal static void AssertImportableModel(AbstractModel model, string path)
    {
        foreach (FieldInfo field in model.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public |
                     BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (field.IsStatic) continue;
            if (!Fields.ContainsKey(Key(field)))
                throw new TransplantMappingException($"{path}.Props.{field.Name}",
                    "Unregistered model field.");
        }
    }
    internal static void AssertExportableModel(AbstractModel model, string path)
    {
        foreach (FieldInfo field in model.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public |
                     BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (field.IsStatic) continue;
            if (!Fields.TryGetValue(Key(field), out var entry))
                throw new TransplantMappingException($"{path}.Props.{field.Name}",
                    "Unregistered model field.");
            if (entry.Class != TransplantFieldClass.Snapshot) continue;
            object? value = field.GetValue(model);
            if (value is null) continue;
            if (value is System.Collections.ICollection collection && collection.Count == 0) continue;
            if (field.FieldType.IsValueType &&
                Equals(value, Activator.CreateInstance(field.FieldType))) continue;
            throw new TransplantMappingException($"{path}.Props.{field.Name}",
                "Non-default model state has no native snapshot mapping.");
        }
    }
    private static string IgnoredReason(Type type, string fieldName) =>
        CombatPrivateStateRegistry.TryGetIgnoredReason(type, fieldName, out string reason)
            ? reason
            : throw new InvalidOperationException($"Missing reason for {type.Name}.{fieldName}.");

    private static string Key(FieldInfo field) => $"{field.DeclaringType!.FullName}.{field.Name}";
}
