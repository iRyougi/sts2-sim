using System.Reflection;
using System.Text.Json;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Map;

namespace Sts2Sim.Core.Runs.Transplant;

/// <summary>Explicit native SavedProperty-name to simulator-field bindings.</summary>
internal static class TransplantSavedProperties
{
    private sealed record Binding(string FieldName, string Group);

    private static readonly IReadOnlyDictionary<string, Binding> Bindings =
        new Dictionary<string, Binding>(StringComparer.Ordinal)
        {
            ["SeaGlass.CharacterId"] = new("_characterId", "model_ids"),
            ["BoneTea.CombatsLeft"] = new("_combatsLeft", "ints"),
            ["BookOfFiveRings.CardsAdded"] = new("_cardsAdded", "ints"),
            ["DustyTome.AncientCard"] = new("_ancientCard", "model_ids"),
            ["EmberTea.CombatsLeft"] = new("_combatsLeft", "ints"),
            ["FakeHappyFlower.TurnsSeen"] = new("_turnsSeen", "ints"),
            ["FakeVenerableTeaSet.GainEnergyInNextCombat"] = new("_isArmed", "bools"),
            ["FurCoat.FurCoatActIndex"] = new("_actIndex", "ints"),
            ["GalacticDust.StarsSpent"] = new("_starsSpent", "ints"),
            ["Girya.TimesLifted"] = new("_timesLifted", "ints"),
            ["HappyFlower.TurnsSeen"] = new("_turnCounter", "ints"),
            ["IronClub.CardsPlayed"] = new("_cardsPlayed", "ints"),
            ["JossPaper.CardsExhausted"] = new("_cardsExhausted", "ints"),
            ["LastingCandy.CombatRewardsSeen"] = new("_combatRewardsSeen", "ints"),
            ["LizardTail.WasUsed"] = new("_wasUsed", "bools"),
            ["MawBank.HasItemBeenBought"] = new("_hasItemBeenBought", "bools"),
            ["Nunchaku.AttacksPlayed"] = new("_attacksPlayed", "ints"),
            ["PaelsWing.RewardsSacrificed"] = new("_rewardsSacrificed", "ints"),
            ["Pendulum.TurnsSeen"] = new("_turnCounter", "ints"),
            ["PenNib.AttacksPlayed"] = new("_attacksPlayed", "ints"),
            ["PollinousCore.TurnsSeen"] = new("_handsSeen", "ints"),
            ["SilkenTress.IsUsed"] = new("_isUsed", "bools"),
            ["SwordOfStone.ElitesDefeated"] = new("_eliteVictories", "ints"),
            ["TeaOfDiscourtesy.CombatsLeft"] = new("_combatsLeft", "ints"),
            ["TuningFork.SkillsPlayed"] = new("_skillsPlayed", "ints"),
            ["VenerableTeaSet.GainEnergyInNextCombat"] = new("_isArmed", "bools"),
            ["WongosMysteryTicket.CombatsFinished"] = new("_combatsFinished", "ints"),
            ["WongosMysteryTicket.GaveRelic"] = new("_gaveRelics", "bools"),
            ["Dowsing.RoomsEntered"] = new("_unknownRoomsEntered", "ints"),
            ["Guilty.CombatsSeen"] = new("_combatsCompleted", "ints"),
            ["FishingRod.CombatsSeen"] = new("<CombatsSeen>k__BackingField", "ints"),
            ["LavaRock.HasTriggered"] = new("<HasTriggered>k__BackingField", "bools"),
            ["LavaLamp.TookDamageThisCombat"] = new("_tookDamageThisCombat", "bools"),
            ["PumpkinCandle.KindleCount"] = new("<KindleCount>k__BackingField", "ints"),
            ["SilverCrucible.TimesUsed"] = new("<TimesUsed>k__BackingField", "ints"),
            ["SilverCrucible.TreasureRoomsEntered"] = new("<TreasureRoomsEntered>k__BackingField", "ints"),
            ["WingedBoots.TimesUsed"] = new("<TimesUsed>k__BackingField", "ints"),
            ["SpoilsMap.SpoilsActIndex"] = new("<SpoilsActIndex>k__BackingField", "ints"),
            ["MadScience.TinkerTimeType"] = new("_type", "ints"),
            ["MadScience.TinkerTimeRider"] = new("<Rider>k__BackingField", "ints"),
        };

    public static IReadOnlyDictionary<string, JsonElement> Export(AbstractModel model, string path)
    {
        TransplantFieldRegistry.AssertImportableModel(model, path);
        var groups = new Dictionary<string, List<object>>(StringComparer.Ordinal);
        if (model is FurCoat furCoat)
        {
            MapCoord[] coords = furCoat.MarkedCoordinates.ToArray();
            groups["int_arrays"] =
            [
                new { name = "FurCoatCoordCols", value = coords.Select(coord => coord.col).ToArray() },
                new { name = "FurCoatCoordRows", value = coords.Select(coord => coord.row).ToArray() },
            ];
            groups["bools"] = [new { name = "FurCoatCoordsSet", value =
                (int)typeof(FurCoat).GetField("_actIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(furCoat)! >= 0 }];
        }
        if (model is PaelsTooth tooth)
        {
            FieldInfo storedField = typeof(PaelsTooth).GetField("_removedCards",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var stored = (List<CardModel>)storedField.GetValue(tooth)!;
            object[] nativeCards = stored.Select((card, index) =>
                ToNativeCard(RunTransplantExporter.ExportCardForTransplant(card,
                    $"{path}.SerializableCards[{index}]"))).ToArray();
            groups["card_arrays"] = [new { name = "SerializableCards", value = nativeCards }];
        }
        foreach (FieldInfo field in model.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public |
                     BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (field.IsStatic) continue;
            string fieldKey = $"{field.DeclaringType!.FullName}.{field.Name}";
            if (model is FurCoat && field.Name == "_markedCoordinates") continue;
            if (model is PaelsTooth && field.Name == "_removedCards") continue;
            if (model is SpoilsMap && field.Name == "<SpoilsCoord>k__BackingField") continue;
            if (TransplantFieldRegistry.Fields[fieldKey].Class != TransplantFieldClass.Snapshot) continue;
            object? value = field.GetValue(model);
            KeyValuePair<string, Binding>? binding = Bindings.FirstOrDefault(pair =>
                pair.Key.StartsWith(model.GetType().Name + ".", StringComparison.Ordinal) &&
                pair.Value.FieldName == field.Name);
            if (binding is null || binding.Value.Key is null)
            {
                if (IsDefault(field.FieldType, value)) continue;
                throw new TransplantMappingException($"{path}.Props.{field.Name}",
                    "Non-default model state has no native saved property binding.");
            }
            if (value is null) continue;
            object savedValue = value is ModelId id ? id.ToString() : value;
            string nativeName = binding.Value.Key[(model.GetType().Name.Length + 1)..];
            if (!groups.TryGetValue(binding.Value.Value.Group, out List<object>? entries))
                groups.Add(binding.Value.Value.Group, entries = []);
            entries.Add(new { name = nativeName, value = savedValue });
        }
        var outer = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (groups.Count > 0)
            outer["props"] = JsonSerializer.SerializeToElement(groups);
        if (model is SpoilsMap spoilsMap)
            outer["spoils_coord"] = spoilsMap.SpoilsCoord is { } coord
                ? JsonSerializer.SerializeToElement(new { col = coord.col, row = coord.row })
                : JsonSerializer.SerializeToElement<object?>(null);
        return outer;
    }

    private static bool IsDefault(Type type, object? value)
    {
        if (value is null) return true;
        if (value is System.Collections.ICollection collection && collection.Count == 0) return true;
        return type.IsValueType && Equals(value, Activator.CreateInstance(type));
    }
    public static void Apply(
        AbstractModel model,
        IReadOnlyDictionary<string, JsonElement> outer,
        string path,
        bool relic)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (outer is null)
            throw new TransplantMappingException($"{path}.Props", "Missing property map.");
        bool sawSpoilsCoord = false;
        foreach ((string key, JsonElement value) in outer)
        {
            if (model is SpoilsMap spoilsMap && key == "spoils_coord")
            {
                ApplySpoilsCoord(spoilsMap, value, $"{path}.Props.spoils_coord");
                sawSpoilsCoord = true;
                continue;
            }
            if (relic && key == "floor_added_to_deck")
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int floor) || floor < 0)
                    throw new TransplantMappingException($"{path}.Props.floor_added_to_deck",
                        "Invalid relic acquisition floor.");
                // Native uses this for history and net digests; the simulator has no matching field.
                continue;
            }
            if (key != "props")
                throw new TransplantMappingException($"{path}.Props.{key}", "Unknown outer property.");
            ApplySavedProperties(model, value, $"{path}.Props.props");
        }
        if (model is SpoilsMap && !sawSpoilsCoord)
            throw new TransplantMappingException($"{path}.Props.spoils_coord",
                "Missing native cross-room coordinate.");
    }

    private static void ApplySpoilsCoord(SpoilsMap card, JsonElement value, string path)
    {
        MapCoord? coord = null;
        if (value.ValueKind != JsonValueKind.Null)
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty("col", out JsonElement colValue) ||
                !value.TryGetProperty("row", out JsonElement rowValue) ||
                value.EnumerateObject().Any(property => property.Name is not ("col" or "row")) ||
                colValue.ValueKind != JsonValueKind.Number ||
                rowValue.ValueKind != JsonValueKind.Number ||
                !colValue.TryGetInt32(out int col) || !rowValue.TryGetInt32(out int row) ||
                col < 0 || row < 0)
                throw new TransplantMappingException(path, "Invalid native SpoilsMap coordinate.");
            coord = new MapCoord(col, row);
        }
        FieldInfo field = typeof(SpoilsMap).GetField("<SpoilsCoord>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new TransplantMappingException(path, "Stale simulator coordinate binding.");
        try { field.SetValue(card, coord); }
        catch (Exception error) when (error is ArgumentException or FieldAccessException or TargetException)
        { throw new TransplantMappingException(path, error.Message); }
    }

    private static void ApplySavedProperties(AbstractModel model, JsonElement save, string path)
    {
        if (save.ValueKind != JsonValueKind.Object)
            throw new TransplantMappingException(path, "SavedProperties must be an object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var furCoordinates = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        JsonElement? paelsCards = null;
        foreach (JsonProperty group in save.EnumerateObject())
        {
            if (group.Name is not ("ints" or "bools" or "strings" or "int_arrays" or
                "model_ids" or "cards" or "card_arrays") || group.Value.ValueKind != JsonValueKind.Array)
                throw new TransplantMappingException($"{path}.{group.Name}", "Unknown property group.");
            foreach (JsonElement entry in group.Value.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("name", out JsonElement nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String ||
                    !entry.TryGetProperty("value", out JsonElement value) ||
                    entry.EnumerateObject().Any(property => property.Name is not ("name" or "value")))
                    throw new TransplantMappingException($"{path}.{group.Name}", "Malformed saved property.");
                string name = nameElement.GetString()!;
                string bindingKey = $"{model.GetType().Name}.{name}";
                if (!seen.Add(bindingKey))
                    throw new TransplantMappingException($"{path}.{name}", "Duplicate saved property.");
                if (model is FurCoat && name is ("FurCoatCoordCols" or "FurCoatCoordRows" or "FurCoatCoordsSet"))
                {
                    string expectedGroup = name == "FurCoatCoordsSet" ? "bools" : "int_arrays";
                    if (group.Name != expectedGroup)
                        throw new TransplantMappingException($"{path}.{name}", "Wrong native property group.");
                    furCoordinates.Add(name, value);
                    continue;
                }
                if (model is PaelsTooth && name == "SerializableCards")
                {
                    if (group.Name != "card_arrays" || value.ValueKind != JsonValueKind.Array)
                        throw new TransplantMappingException($"{path}.{name}", "Invalid native card array.");
                    paelsCards = value;
                    continue;
                }
                if (name == "Skin" && model is Byrdpip or PaelsLegion)
                {
                    // new[] instead of collection expressions: older 9.0.1xx compilers parse
                    // "is Byrdpip ? [" as the nullable array type Byrdpip?[] (#187).
                    string[] options = model is Byrdpip
                        ? new[] { "version1", "version2", "version3", "version4" }
                        : new[] { "eyes", "horns", "spikes", "wings" };
                    if (group.Name != "strings" || value.ValueKind != JsonValueKind.String ||
                        !options.Contains(value.GetString(), StringComparer.Ordinal))
                        throw new TransplantMappingException($"{path}.{name}",
                            "Invalid native presentation skin.");
                    continue;
                }
                if (!Bindings.TryGetValue(bindingKey, out Binding? binding) || binding.Group != group.Name)
                    throw new TransplantMappingException($"{path}.{name}", "Unmapped native saved property.");
                FieldInfo field = model.GetType().GetField(binding.FieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.DeclaredOnly) ??
                    throw new TransplantMappingException($"{path}.{name}", "Stale simulator field binding.");
                object decoded;
                try
                {
                    decoded = group.Name switch
                    {
                        "ints" when field.FieldType.IsEnum => Enum.ToObject(field.FieldType, value.GetInt32()),
                        "ints" => value.GetInt32(),
                        "bools" => value.GetBoolean(),
                        "strings" => value.GetString() ?? throw new JsonException("Null string."),
                        "model_ids" => ModelId.Deserialize(value.GetString() ??
                            throw new JsonException("Null ModelId.")),
                        _ => throw new TransplantMappingException($"{path}.{name}",
                            "Complex native saved property needs a dedicated mapping."),
                    };
                }
                catch (Exception error) when (error is JsonException or FormatException or
                    InvalidOperationException or OverflowException or ArgumentException)
                {
                    throw new TransplantMappingException($"{path}.{name}", error.Message);
                }
                if (!field.FieldType.IsInstanceOfType(decoded))
                    throw new TransplantMappingException($"{path}.{name}", "Saved property type mismatch.");
                try { field.SetValue(model, decoded); }
                catch (Exception error) when (error is ArgumentException or FieldAccessException or TargetException)
                { throw new TransplantMappingException($"{path}.{name}", error.Message); }
            }
        }
        if (model is FurCoat coat && furCoordinates.Count > 0)
            ApplyFurCoatCoordinates(coat, furCoordinates, path);
        if (model is PaelsTooth storedTooth && paelsCards is { } cards)
        {
            var restored = new List<CardModel>();
            int index = 0;
            foreach (JsonElement card in cards.EnumerateArray())
            {
                string cardPath = $"{path}.SerializableCards[{index++}]";
                restored.Add(storedTooth.Owner.CreateCardForTransplant(
                    ReadNativeCard(card, cardPath), cardPath));
            }
            typeof(PaelsTooth).GetField("_removedCards",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(storedTooth, restored);
        }
    }

    private static object ToNativeCard(CardSnapshot card)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = card.Id,
            ["current_upgrade_level"] = card.UpgradeLevel,
        };
        if (card.FloorAddedToDeck is not null)
            result["floor_added_to_deck"] = card.FloorAddedToDeck;
        if (card.Props.TryGetValue("props", out JsonElement properties))
            result["props"] = properties;
        if (card.Props.TryGetValue("spoils_coord", out JsonElement spoilsCoord))
            result["spoils_coord"] = spoilsCoord;
        if (card.EnchantmentId is not null)
            result["enchantment"] = new
            {
                id = card.EnchantmentId,
                amount = card.EnchantmentAmount
            };
        return result;
    }

    private static CardSnapshot ReadNativeCard(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new TransplantMappingException(path, "Stored card must be an object.");
        foreach (JsonProperty property in value.EnumerateObject())
            if (property.Name is not ("id" or "current_upgrade_level" or "enchantment" or
                "props" or "floor_added_to_deck" or "spoils_coord"))
                throw new TransplantMappingException($"{path}.{property.Name}", "Unknown stored-card property.");
        if (!value.TryGetProperty("id", out JsonElement idValue) ||
            idValue.ValueKind != JsonValueKind.String)
            throw new TransplantMappingException($"{path}.id", "Missing stored-card ID.");
        string id = idValue.GetString()!;
        int level = 0;
        if (value.TryGetProperty("current_upgrade_level", out JsonElement upgrade) &&
            (upgrade.ValueKind != JsonValueKind.Number || !upgrade.TryGetInt32(out level)))
            throw new TransplantMappingException($"{path}.current_upgrade_level", "Invalid upgrade level.");
        int? floor = null;
        if (value.TryGetProperty("floor_added_to_deck", out JsonElement floorValue))
        {
            if (floorValue.ValueKind != JsonValueKind.Number || !floorValue.TryGetInt32(out int parsedFloor))
                throw new TransplantMappingException($"{path}.floor_added_to_deck", "Invalid acquisition floor.");
            floor = parsedFloor;
        }
        string? enchantmentId = null;
        int? enchantmentAmount = null;
        if (value.TryGetProperty("enchantment", out JsonElement enchantment) &&
            enchantment.ValueKind != JsonValueKind.Null)
        {
            if (enchantment.ValueKind != JsonValueKind.Object)
                throw new TransplantMappingException($"{path}.enchantment", "Invalid enchantment.");
            foreach (JsonProperty property in enchantment.EnumerateObject())
                if (property.Name is not ("id" or "amount" or "props"))
                    throw new TransplantMappingException($"{path}.enchantment.{property.Name}",
                        "Unknown enchantment property.");
            if (!enchantment.TryGetProperty("id", out JsonElement enchantmentIdValue) ||
                enchantmentIdValue.ValueKind != JsonValueKind.String ||
                !enchantment.TryGetProperty("amount", out JsonElement enchantmentAmountValue) ||
                enchantmentAmountValue.ValueKind != JsonValueKind.Number ||
                !enchantmentAmountValue.TryGetInt32(out int parsedAmount))
                throw new TransplantMappingException($"{path}.enchantment", "Incomplete enchantment.");
            enchantmentId = enchantmentIdValue.GetString();
            enchantmentAmount = parsedAmount;
            if (enchantment.TryGetProperty("props", out JsonElement enchantmentProps) &&
                enchantmentProps.ValueKind != JsonValueKind.Null &&
                (enchantmentProps.ValueKind != JsonValueKind.Object ||
                    enchantmentProps.EnumerateObject().Any()))
                throw new TransplantMappingException($"{path}.enchantment.props",
                    "Stored enchantment has unmapped properties.");
        }
        var props = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (value.TryGetProperty("props", out JsonElement saved) &&
            saved.ValueKind != JsonValueKind.Null)
        {
            if (saved.ValueKind != JsonValueKind.Object)
                throw new TransplantMappingException($"{path}.props", "Invalid stored-card properties.");
            props["props"] = saved.Clone();
        }
        if (value.TryGetProperty("spoils_coord", out JsonElement coord))
            props["spoils_coord"] = coord.Clone();
        return new CardSnapshot(id, level, enchantmentId, enchantmentAmount, floor, props);
    }
    private static void ApplyFurCoatCoordinates(
        FurCoat coat, IReadOnlyDictionary<string, JsonElement> values, string path)
    {
        if (!values.TryGetValue("FurCoatCoordsSet", out JsonElement setValue) ||
            setValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new TransplantMappingException($"{path}.FurCoatCoordsSet", "Missing coordinate flag.");
        int[] cols = ReadCoordinates(values, "FurCoatCoordCols", path);
        int[] rows = ReadCoordinates(values, "FurCoatCoordRows", path);
        if (cols.Length != rows.Length || (setValue.GetBoolean() && cols.Length == 0) ||
            (!setValue.GetBoolean() && cols.Length != 0))
            throw new TransplantMappingException(path, "FurCoat coordinate arrays disagree.");
        var coords = new HashSet<MapCoord>();
        for (int index = 0; index < cols.Length; index++)
            if (!coords.Add(new MapCoord(cols[index], rows[index])))
                throw new TransplantMappingException($"{path}.FurCoatCoordCols[{index}]",
                    "Duplicate FurCoat coordinate.");
        typeof(FurCoat).GetField("_markedCoordinates",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(coat, coords);
    }

    private static int[] ReadCoordinates(
        IReadOnlyDictionary<string, JsonElement> values, string name, string path)
    {
        if (!values.TryGetValue(name, out JsonElement array))
            return [];
        if (array.ValueKind != JsonValueKind.Array)
            throw new TransplantMappingException($"{path}.{name}", "Coordinate value must be an array.");
        try { return array.EnumerateArray().Select(item => item.GetInt32()).ToArray(); }
        catch (Exception error) when (error is InvalidOperationException or FormatException or OverflowException)
        { throw new TransplantMappingException($"{path}.{name}", error.Message); }
    }
}
