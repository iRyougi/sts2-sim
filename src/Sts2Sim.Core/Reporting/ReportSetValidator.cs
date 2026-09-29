using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Reporting;

/// <summary>Validates the shared in-memory contract consumed by every report renderer.</summary>
internal static class ReportSetValidator
{
    private const int MaximumCombatIdLength = 128;
    private static readonly HashSet<string> ReservedWindowsDeviceNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        ],
        StringComparer.OrdinalIgnoreCase);

    public static void Validate(
        RunManifest manifest,
        IReadOnlyDictionary<string, CombatLog> combatLogs)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(combatLogs);

        RequireString(manifest.SchemaVersion, "Manifest schema version", nameof(manifest));
        RequireString(manifest.RunId, "Manifest run ID", nameof(manifest));
        RequireString(manifest.Seed, "Manifest seed", nameof(manifest));
        RequireString(manifest.Character, "Manifest character", nameof(manifest));
        RequireString(manifest.Result, "Manifest result", nameof(manifest));

        if (!string.Equals(
                manifest.SchemaVersion,
                ReportingSchema.CurrentVersion,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Manifest schema version must be '{ReportingSchema.CurrentVersion}'.",
                nameof(manifest));
        }
        if (manifest.Result is not ("victory" or "defeat"))
        {
            throw new ArgumentException(
                $"Manifest has unsupported result '{manifest.Result}'.",
                nameof(manifest));
        }

        IReadOnlyList<FloorEntry> floors = manifest.Floors ??
            throw new ArgumentException("Manifest floors must not be null.", nameof(manifest));
        if (manifest.FloorsVisited != floors.Count)
        {
            throw new ArgumentException(
                "Manifest floors_visited must equal the number of floor entries.",
                nameof(manifest));
        }

        for (int index = 0; index < floors.Count; index++)
        {
            FloorEntry floor = floors[index];
            ValidateFloorGraph(floor, index, nameof(manifest));
            if (floor.FloorIndex != index + 1)
            {
                throw new ArgumentException(
                    "Manifest floor indexes must be continuous and start at 1.",
                    nameof(manifest));
            }

            if (floor.DeckBefore is null ||
                floor.DeckAfter is null ||
                floor.RelicsBefore is null ||
                floor.RelicsAfter is null ||
                floor.PotionsBefore is null ||
                floor.PotionsAfter is null)
            {
                throw new ArgumentException(
                    $"Manifest floor {floor.FloorIndex} inventory snapshot collections must not be null.",
                    nameof(manifest));
            }
        }
        if (manifest.EndedAtUtc < manifest.StartedAtUtc)
        {
            throw new ArgumentException(
                "Manifest ended_at_utc must not precede started_at_utc.",
                nameof(manifest));
        }

        IReadOnlyList<CombatLogRef> references = manifest.CombatLogs ??
            throw new ArgumentException("Manifest combat log index must not be null.", nameof(manifest));
        var referencesById = new Dictionary<string, CombatLogRef>(StringComparer.Ordinal);
        var canonicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CombatLogRef reference in references)
        {
            Required(reference, "Manifest combat log index entry", nameof(manifest));
            RequireString(reference.CombatId, "Manifest combat log index combat ID", nameof(manifest));
            RequireString(reference.CombatLogFile, "Manifest combat log index file", nameof(manifest));

            ValidateCombatId(reference.CombatId);
            string expectedPath = CanonicalCombatPath(reference.Floor, reference.CombatId);
            if (!string.Equals(reference.CombatLogFile, expectedPath, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Combat index path for '{reference.CombatId}' must be '{expectedPath}'.",
                    nameof(manifest));
            }
            if (!canonicalPaths.Add(expectedPath))
            {
                throw new ArgumentException(
                    $"Combat index path '{expectedPath}' collides on a case-insensitive filesystem.",
                    nameof(manifest));
            }
            if (!referencesById.TryAdd(reference.CombatId, reference))
            {
                throw new ArgumentException(
                    $"Manifest combat log index contains duplicate ID '{reference.CombatId}'.",
                    nameof(manifest));
            }
        }

        if (referencesById.Count != combatLogs.Count)
        {
            CombatLogRef? missing = references.FirstOrDefault(
                reference => !combatLogs.ContainsKey(reference.CombatId));
            if (missing is not null)
            {
                throw new ArgumentException(
                    $"Missing indexed combat log '{missing.CombatId}'.",
                    nameof(combatLogs));
            }

            throw new ArgumentException(
                "Manifest combat log index and supplied logs must have a one-to-one correspondence.",
                nameof(combatLogs));
        }

        var logsById = new Dictionary<string, CombatLog>(StringComparer.Ordinal);
        foreach ((string key, CombatLog log) in combatLogs)
        {
            ArgumentNullException.ThrowIfNull(log);
            ValidateCombatGraph(log);
            ValidateCombatId(log.CombatId);
            if (!string.Equals(key, log.CombatId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Combat log dictionary key '{key}' does not match combat ID '{log.CombatId}'.",
                    nameof(combatLogs));
            }
            if (!referencesById.TryGetValue(log.CombatId, out CombatLogRef? reference) ||
                reference.Floor != log.Floor)
            {
                throw new ArgumentException(
                    $"Combat log '{log.CombatId}' is missing from the index or has a different floor.",
                    nameof(combatLogs));
            }
            if (!string.Equals(log.RunId, manifest.RunId, StringComparison.Ordinal) ||
                !string.Equals(log.SchemaVersion, ReportingSchema.CurrentVersion, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Combat log '{log.CombatId}' must match the manifest run ID and current schema version.",
                    nameof(combatLogs));
            }
            if (log.EndedAtUtc < log.StartedAtUtc)
            {
                throw new ArgumentException(
                    $"Combat log '{log.CombatId}' ended_at_utc must not precede started_at_utc.",
                    nameof(combatLogs));
            }
            if (log.Result is not ("victory" or "defeat"))
            {
                throw new ArgumentException(
                    $"Combat log '{log.CombatId}' has unsupported result '{log.Result}'.",
                    nameof(combatLogs));
            }

            logsById.Add(log.CombatId, log);
        }

        var linkedCombatDetails = new HashSet<string>(StringComparer.Ordinal);
        foreach (FloorEntry floor in floors)
        {
            if (floor.Detail is not CombatFloorDetail detail)
            {
                continue;
            }

            ValidateCombatId(detail.CombatId);
            if (!linkedCombatDetails.Add(detail.CombatId) ||
                !referencesById.TryGetValue(detail.CombatId, out CombatLogRef? reference) ||
                !logsById.TryGetValue(detail.CombatId, out CombatLog? log))
            {
                throw new ArgumentException(
                    $"Combat floor detail '{detail.CombatId}' must link to exactly one indexed combat log.",
                    nameof(manifest));
            }

            string expectedPath = CanonicalCombatPath(floor.FloorIndex, detail.CombatId);
            bool expectedVictory = log.Result == "victory";
            if (reference.Floor != floor.FloorIndex ||
                log.Floor != floor.FloorIndex ||
                !string.Equals(reference.CombatLogFile, expectedPath, StringComparison.Ordinal) ||
                !string.Equals(detail.CombatLogFile, expectedPath, StringComparison.Ordinal) ||
                detail.Victory != expectedVictory)
            {
                throw new ArgumentException(
                    $"Combat floor detail '{detail.CombatId}' must match its index, floor, path, and result.",
                    nameof(manifest));
            }
        }

        foreach (CombatLog log in logsById.Values)
        {
            if (linkedCombatDetails.Contains(log.CombatId))
            {
                continue;
            }

            bool isForcedCombat =
                string.Equals(log.EncounterName, CombatRoom.ForcedEncounterName, StringComparison.Ordinal) &&
                floors.Any(floor =>
                    floor.FloorIndex == log.Floor &&
                    floor.Detail is EventFloorDetail or AncientFloorDetail);
            if (!isForcedCombat)
            {
                throw new ArgumentException(
                    $"Ordinary combat log '{log.CombatId}' must be linked by one combat floor detail.",
                    nameof(manifest));
            }
        }
    }

    private static void ValidateFloorGraph(FloorEntry floor, int index, string parameterName)
    {
        Required(floor, $"Manifest floor entry {index + 1}", parameterName);
        Required(floor.Coord, $"Manifest floor {index + 1} coord", parameterName);
        FloorDetail detail = Required(
            floor.Detail, $"Manifest floor {index + 1} detail", parameterName);
        RequireString(floor.PointType, $"Manifest floor {index + 1} point type", parameterName);
        RequireString(floor.RoomType, $"Manifest floor {index + 1} room type", parameterName);
        ValidateRequiredStrings(
            floor.DeckBefore, $"Manifest floor {index + 1} deck_before", parameterName);
        ValidateRequiredStrings(
            floor.DeckAfter, $"Manifest floor {index + 1} deck_after", parameterName);
        RequireCollection(
            floor.RelicsBefore, $"Manifest floor {index + 1} relics_before", parameterName);
        RequireCollection(
            floor.RelicsAfter, $"Manifest floor {index + 1} relics_after", parameterName);
        RequireCollection(
            floor.PotionsBefore, $"Manifest floor {index + 1} potions_before", parameterName);
        RequireCollection(
            floor.PotionsAfter, $"Manifest floor {index + 1} potions_after", parameterName);

        switch (detail)
        {
            case CombatFloorDetail combat:
                RequireString(combat.CombatId, $"Manifest floor {index + 1} combat ID", parameterName);
                RequireString(combat.CombatLogFile, $"Manifest floor {index + 1} combat log file", parameterName);
                break;
            case RestSiteFloorDetail rest:
                RequireString(rest.Decision, $"Manifest floor {index + 1} rest decision", parameterName);
                break;
            case ShopFloorDetail shop:
                IReadOnlyList<PurchaseRecord> purchases = RequireCollection(
                    shop.Purchases, $"Manifest floor {index + 1} purchases", parameterName);
                for (int purchaseIndex = 0; purchaseIndex < purchases.Count; purchaseIndex++)
                {
                    PurchaseRecord purchase = Required(
                        purchases[purchaseIndex],
                        $"Manifest floor {index + 1} purchases[{purchaseIndex}]",
                        parameterName);
                    RequireString(
                        purchase.Kind,
                        $"Manifest floor {index + 1} purchases[{purchaseIndex}] kind",
                        parameterName);
                    RequireString(
                        purchase.Id,
                        $"Manifest floor {index + 1} purchases[{purchaseIndex}] id",
                        parameterName);
                }
                break;
            case TreasureFloorDetail:
                break;
            case EventFloorDetail eventDetail:
                RequireString(eventDetail.EventName, $"Manifest floor {index + 1} event name", parameterName);
                RequireString(eventDetail.OptionChosen, $"Manifest floor {index + 1} event option", parameterName);
                RequireString(eventDetail.EffectsSummary, $"Manifest floor {index + 1} event effects", parameterName);
                break;
            case AncientFloorDetail ancient:
                RequireString(ancient.EventName, $"Manifest floor {index + 1} ancient name", parameterName);
                RequireString(ancient.OptionChosen, $"Manifest floor {index + 1} ancient option", parameterName);
                break;
            default:
                throw new ArgumentException(
                    $"Manifest floor {index + 1} has an unsupported detail type.",
                    parameterName);
        }
    }

    private static void ValidateCombatGraph(CombatLog log)
    {
        const string parameterName = "combatLogs";
        Required(log, "Supplied combat log", parameterName);
        string path = $"Combat log '{log.CombatId ?? "<null>"}'";
        RequireString(log.SchemaVersion, $"{path} schema version", parameterName);
        RequireString(log.RunId, $"{path} run ID", parameterName);
        RequireString(log.CombatId, $"{path} combat ID", parameterName);
        RequireString(log.EncounterType, $"{path} encounter type", parameterName);
        RequireString(log.EncounterName, $"{path} encounter name", parameterName);
        RequireString(log.Result, $"{path} result", parameterName);

        CombatPlayerInitialSnapshot playerInitial = Required(
            log.PlayerInitial, $"{path} player_initial", parameterName);
        RequireCollection(playerInitial.RelicIds, $"{path} player_initial relic_ids", parameterName);
        RequireCollection(playerInitial.PotionIds, $"{path} player_initial potion_ids", parameterName);

        IReadOnlyList<CombatEnemyInitialSnapshot> enemiesInitial = RequireCollection(
            log.EnemiesInitial, $"{path} enemies_initial", parameterName);
        for (int index = 0; index < enemiesInitial.Count; index++)
        {
            CombatEnemyInitialSnapshot enemy = Required(
                enemiesInitial[index], $"{path} enemies_initial[{index}]", parameterName);
            RequireString(enemy.Slot, $"{path} enemies_initial[{index}] slot", parameterName);
            RequireString(enemy.Id, $"{path} enemies_initial[{index}] id", parameterName);
            ValidatePowerSnapshots(
                enemy.Powers, $"{path} enemies_initial[{index}] powers", parameterName);
        }

        IReadOnlyList<TurnRecord> turns = RequireCollection(log.Turns, $"{path} turns", parameterName);
        for (int index = 0; index < turns.Count; index++)
        {
            ValidateTurn(
                Required(turns[index], $"{path} turns[{index}]", parameterName),
                $"{path} turns[{index}]",
                parameterName);
        }

        CombatRewards rewards = Required(log.Rewards, $"{path} rewards", parameterName);
        ValidateRequiredStrings(rewards.CardsOffered, $"{path} rewards cards offered", parameterName);
        ValidateRequiredStrings(rewards.CardsTaken, $"{path} rewards cards taken", parameterName);
        ValidateRequiredStrings(rewards.RelicsTaken, $"{path} rewards relics taken", parameterName);
        ValidateRequiredStrings(rewards.PotionsTaken, $"{path} rewards potions taken", parameterName);
    }

    private static void ValidateTurn(TurnRecord turn, string path, string parameterName)
    {
        RequireString(turn.Side, $"{path} side", parameterName);
        ValidatePlayerSnapshot(
            Required(turn.PlayerPre, $"{path} player_pre", parameterName),
            $"{path} player_pre",
            parameterName);
        ValidateEnemySnapshots(turn.EnemiesPre, $"{path} enemies_pre", parameterName);

        IReadOnlyList<DrawRecord> draws = RequireCollection(turn.Draws, $"{path} draws", parameterName);
        for (int index = 0; index < draws.Count; index++)
        {
            DrawRecord draw = Required(draws[index], $"{path} draws[{index}]", parameterName);
            RequireString(draw.Card, $"{path} draws[{index}] card", parameterName);
        }

        IReadOnlyList<ActionRecord> actions = RequireCollection(
            turn.Actions, $"{path} actions", parameterName);
        for (int index = 0; index < actions.Count; index++)
        {
            ValidateAction(
                Required(actions[index], $"{path} actions[{index}]", parameterName),
                $"{path} actions[{index}]",
                parameterName);
        }

        ValidatePlayerSnapshot(
            Required(turn.PlayerPost, $"{path} player_post", parameterName),
            $"{path} player_post",
            parameterName);
        ValidateEnemySnapshots(turn.EnemiesPost, $"{path} enemies_post", parameterName);
    }

    private static void ValidateAction(ActionRecord action, string path, string parameterName)
    {
        switch (action)
        {
            case PlayCardAction card:
                RequireString(card.Card, $"{path} card", parameterName);
                ValidateDamageDealt(card.DamageDealt, $"{path} damage_dealt", parameterName);
                ValidatePowerApplications(card.PowersApplied, $"{path} powers_applied", parameterName);
                break;
            case UsePotionAction potion:
                RequireString(potion.Potion, $"{path} potion", parameterName);
                ValidateDamageDealt(potion.DamageDealt, $"{path} damage_dealt", parameterName);
                ValidatePowerApplications(potion.PowersApplied, $"{path} powers_applied", parameterName);
                ValidateRequiredStrings(potion.PotionsAdded, $"{path} potions_added", parameterName);
                ValidateRequiredStrings(potion.PotionsRemoved, $"{path} potions_removed", parameterName);
                break;
            case EnemyAction enemy:
                RequireString(enemy.Source, $"{path} source", parameterName);
                RequireString(enemy.MoveId, $"{path} move_id", parameterName);
                IReadOnlyList<DamageTakenRecord> damage = RequireCollection(
                    enemy.DamageToTargets, $"{path} damage_to_targets", parameterName);
                ValidateRequiredElements(damage, $"{path} damage_to_targets", parameterName);
                ValidatePowerApplications(enemy.PowersApplied, $"{path} powers_applied", parameterName);
                break;
            case EndTurnAction:
                break;
            default:
                throw new ArgumentException($"{path} has an unsupported action type.", parameterName);
        }
    }

    private static void ValidatePlayerSnapshot(PlayerSnapshot player, string path, string parameterName)
    {
        ValidateRequiredStrings(player.Hand, $"{path} hand", parameterName);
        ValidatePowerSnapshots(player.Powers, $"{path} powers", parameterName);
    }

    private static void ValidateEnemySnapshots(
        IReadOnlyList<EnemySnapshot>? enemies,
        string path,
        string parameterName)
    {
        IReadOnlyList<EnemySnapshot> required = RequireCollection(enemies, path, parameterName);
        for (int index = 0; index < required.Count; index++)
        {
            EnemySnapshot enemy = Required(required[index], $"{path}[{index}]", parameterName);
            RequireString(enemy.Slot, $"{path}[{index}] slot", parameterName);
            RequireString(enemy.Id, $"{path}[{index}] id", parameterName);
            RequireString(enemy.Intent, $"{path}[{index}] intent", parameterName);
            ValidatePowerSnapshots(enemy.Powers, $"{path}[{index}] powers", parameterName);
        }
    }

    private static void ValidatePowerSnapshots(
        IReadOnlyList<PowerSnapshot>? powers,
        string path,
        string parameterName)
    {
        IReadOnlyList<PowerSnapshot> required = RequireCollection(powers, path, parameterName);
        for (int index = 0; index < required.Count; index++)
        {
            PowerSnapshot power = Required(required[index], $"{path}[{index}]", parameterName);
            RequireString(power.Id, $"{path}[{index}] id", parameterName);
        }
    }

    private static void ValidateDamageDealt(
        IReadOnlyList<DamageDealtRecord>? damage,
        string path,
        string parameterName)
    {
        IReadOnlyList<DamageDealtRecord> required = RequireCollection(damage, path, parameterName);
        for (int index = 0; index < required.Count; index++)
        {
            DamageDealtRecord hit = Required(required[index], $"{path}[{index}]", parameterName);
            RequireString(hit.Target, $"{path}[{index}] target", parameterName);
        }
    }

    private static void ValidatePowerApplications(
        IReadOnlyList<PowerApplicationRecord>? powers,
        string path,
        string parameterName)
    {
        IReadOnlyList<PowerApplicationRecord> required = RequireCollection(powers, path, parameterName);
        for (int index = 0; index < required.Count; index++)
        {
            PowerApplicationRecord power = Required(required[index], $"{path}[{index}]", parameterName);
            RequireString(power.Target, $"{path}[{index}] target", parameterName);
            RequireString(power.Power, $"{path}[{index}] power", parameterName);
        }
    }

    private static void ValidateRequiredStrings(
        IReadOnlyList<string>? values,
        string path,
        string parameterName)
    {
        IReadOnlyList<string> required = RequireCollection(values, path, parameterName);
        for (int index = 0; index < required.Count; index++)
        {
            RequireString(required[index], $"{path}[{index}]", parameterName);
        }
    }

    private static void ValidateRequiredElements<T>(
        IReadOnlyList<T> values,
        string path,
        string parameterName)
        where T : class
    {
        for (int index = 0; index < values.Count; index++)
        {
            Required(values[index], $"{path}[{index}]", parameterName);
        }
    }

    private static IReadOnlyList<T> RequireCollection<T>(
        IReadOnlyList<T>? values,
        string path,
        string parameterName) =>
        Required(values, path, parameterName);

    private static string RequireString(string? value, string path, string parameterName) =>
        Required(value, path, parameterName);

    private static T Required<T>(T? value, string path, string parameterName)
        where T : class =>
        value ?? throw new ArgumentException($"{path} must not be null.", parameterName);

    private static string CanonicalCombatPath(int floor, string combatId) =>
        $"combats/{floor:D3}_{combatId}.json";

    private static void ValidateCombatId(string combatId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(combatId);
        if (combatId.Length > MaximumCombatIdLength ||
            combatId[^1] is '.' or ' ' ||
            combatId.Any(character => !IsPortableFileNameCharacter(character)))
        {
            throw new ArgumentException(
                $"Combat ID must contain 1-{MaximumCombatIdLength} portable ASCII file-name characters.",
                nameof(combatId));
        }

        string deviceStem = combatId.Split('.', 2)[0];
        if (ReservedWindowsDeviceNames.Contains(deviceStem))
        {
            throw new ArgumentException(
                "Combat ID must not be a reserved Windows device name.",
                nameof(combatId));
        }
    }

    private static bool IsPortableFileNameCharacter(char character) =>
        character is >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or
            >= '0' and <= '9' or
            '-' or '_' or '.';
}
