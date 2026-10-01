using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Exceptions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Runs.Transplant;

public static class RunTransplantImporter
{
    public static RunState Import(RunTransplantSnapshot snapshot)
    {
        if (snapshot is null)
            throw new TransplantMappingException("RunTransplantSnapshot", "Missing snapshot.");
        if (snapshot.SchemaVersion != RunTransplantSnapshot.CurrentSchemaVersion)
            throw new TransplantMappingException("SchemaVersion", "Unsupported schema version.");
        if (string.IsNullOrWhiteSpace(snapshot.Seed))
            throw new TransplantMappingException("Seed", "Missing seed.");
        if (snapshot.Player is null)
            throw new TransplantMappingException("Player", "Missing player.");
        if (!snapshot.IsInjected && snapshot.TargetNode is null)
            throw new TransplantMappingException("TargetNode", "Missing target.");
        if (snapshot.IsInjected && snapshot.TargetNode is not null)
            throw new TransplantMappingException("TargetNode",
                "Injected combat cannot carry a map target.");
        if (!Enum.TryParse(snapshot.TargetRoomType, false, out RoomType roomType) ||
            roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
            throw new TransplantMappingException("TargetRoomType", "Unknown combat room type.");
        if (snapshot.ExtraFields is null)
            throw new TransplantMappingException("ExtraFields", "Missing extra-field map.");
        ValidateExtraFields(snapshot.ExtraFields);
        ValidateRunRng(snapshot.RunRng, snapshot.Seed);
        ValidatePlayerRng(snapshot.Player.PlayerRng);
        if (snapshot.Player.PlayerOdds is null)
            throw new TransplantMappingException("Player.PlayerOdds", "Missing player odds.");
        if (snapshot.RunOdds is null)
            throw new TransplantMappingException("RunOdds", "Missing run odds.");
        if (snapshot.SharedRelicBag is null)
            throw new TransplantMappingException("SharedRelicBag", "Missing shared relic bag.");
        if (snapshot.Ascension is < 0 or > 10)
            throw new TransplantMappingException("Ascension", "Unsupported ascension level.");
        IReadOnlyList<ActDefinition> acts = ResolveActs(snapshot.ActIds);
        RunState run = new(snapshot.Seed, acts, snapshot.Ascension);
        if (snapshot.Player.PlayerRng.Seed != run.Rng.Seed)
            throw new TransplantMappingException("Player.PlayerRng.Seed", "Player RNG seed differs from run seed.");
        if (string.IsNullOrWhiteSpace(snapshot.CharacterId))
            throw new TransplantMappingException("CharacterId", "Missing character ID.");
        CharacterModel character;
        try { character = ModelDb.GetById<CharacterModel>(ModelId.Deserialize(snapshot.CharacterId)); }
        catch (Exception error) when (error is System.Text.Json.JsonException or
            ModelNotFoundException or InvalidCastException)
        { throw new TransplantMappingException("CharacterId", error.Message); }
        Player player = Player.CreateForNewRun(character, run);
        run.AddPlayer(player);
        if (snapshot.IsInjected)
        {
            // The native scenario host switches acts before SetPlayer replaces the deck.
            run.RestoreForTransplant(snapshot, roomType);
            player.RestoreForTransplant(snapshot.Player);
            run.RestoreSharedRelicBagForTransplant(snapshot.SharedRelicBag);
            if (player.Relics.OfType<GoldenCompass>()
                    .Any(compass => compass.GoldenPathAct == run.CurrentActIndex))
                run.RegenerateCurrentMap();
        }
        else
        {
            // Ordinary runs generate the target-act map with the live deck's map hooks.
            player.RestoreForTransplant(snapshot.Player);
            run.RestoreSharedRelicBagForTransplant(snapshot.SharedRelicBag);
            if (snapshot.ActIndex == 0 && player.Relics.OfType<GoldenCompass>()
                    .Any(compass => compass.GoldenPathAct == 0))
                run.RegenerateCurrentMap();
            var spoils = player.Deck.Cards.Select((card, index) => (card, index))
                .Where(item => item.card is SpoilsMap)
                .Select(item => (item.index, ((SpoilsMap)item.card).SpoilsCoord)).ToArray();
            run.RestoreForTransplant(snapshot, roomType);
            foreach (var (index, expected) in spoils)
                if (((SpoilsMap)player.Deck.Cards[index]).SpoilsCoord != expected)
                    throw new TransplantMappingException($"Player.Deck[{index}].Props.spoils_coord",
                        "Regenerated target-act map changed the native SpoilsMap coordinate.");
        }
        return run;
    }

    private static void ValidateExtraFields(IReadOnlyDictionary<string, System.Text.Json.JsonElement> fields)
    {
        foreach ((string name, System.Text.Json.JsonElement value) in fields)
        {
            string path = $"ExtraFields.{name}";
            switch (name)
            {
                case "started_with_neow":
                    if (value.ValueKind != System.Text.Json.JsonValueKind.True)
                        throw new TransplantMappingException(path,
                            "Full-unlock runs must have started with Neow.");
                    break;
                case "Player.damage_dealt":
                case "Player.debuffs_applied":
                    if (value.ValueKind != System.Text.Json.JsonValueKind.Number ||
                        !value.TryGetInt64(out long count) || count < 0)
                        throw new TransplantMappingException(path, "Invalid historical badge counter.");
                    // Native reads these only for badges and run history, not combat entry behavior.
                    break;
                default:
                    throw new TransplantMappingException(path, "Unmapped native extra field.");
            }
        }
    }
    private static IReadOnlyList<ActDefinition> ResolveActs(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
            throw new TransplantMappingException("ActIds", "Missing act IDs.");
        var acts = new List<ActDefinition>();
        for (int index = 0; index < ids.Count; index++)
        {
            ActDefinition act = ids[index] switch
            {
                nameof(Overgrowth) => new Overgrowth(),
                nameof(Underdocks) => new Underdocks(),
                nameof(Hive) => new Hive(),
                nameof(Glory) => new Glory(),
                _ => throw new TransplantMappingException($"ActIds[{index}]", "Unknown act ID."),
            };
            if (act.Index != index)
                throw new TransplantMappingException($"ActIds[{index}]", "Act has wrong index.");
            acts.Add(act);
        }
        return acts;
    }

    private static void ValidateRunRng(SerializableRunRngSet? rng, string seed)
    {
        if (rng is null || rng.Rngs is null || rng.Seed != seed)
            throw new TransplantMappingException("RunRng", "Missing or mismatched run RNG.");
        foreach (RunRngType type in Enum.GetValues<RunRngType>())
            if (!rng.Rngs.TryGetValue(type, out SerializableRng? state) || state is null)
                throw new TransplantMappingException($"RunRng.Rngs.{type}", "Missing RNG stream.");
        if (rng.Rngs.Count != Enum.GetValues<RunRngType>().Length)
            throw new TransplantMappingException("RunRng.Rngs", "Unknown RNG stream.");
    }

    private static void ValidatePlayerRng(SerializablePlayerRngSet? rng)
    {
        if (rng is null || rng.Rngs is null)
            throw new TransplantMappingException("Player.PlayerRng", "Missing player RNG.");
        foreach (PlayerRngType type in Enum.GetValues<PlayerRngType>())
            if (!rng.Rngs.TryGetValue(type, out SerializableRng? state) || state is null)
                throw new TransplantMappingException($"Player.PlayerRng.Rngs.{type}", "Missing RNG stream.");
        if (rng.Rngs.Count != Enum.GetValues<PlayerRngType>().Length)
            throw new TransplantMappingException("Player.PlayerRng.Rngs", "Unknown RNG stream.");
    }
}
