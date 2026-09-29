using System.Text.Json;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Runs.Transplant;

public static class RunTransplantExporter
{
    internal static CardSnapshot ExportCardForTransplant(CardModel card, string path)
    {
        IReadOnlyDictionary<string, JsonElement> cardProps = TransplantSavedProperties.Export(card, path);
        if (card.Enchantments.Count > 1)
            throw new TransplantMappingException($"{path}.EnchantmentId",
                "Multiple enchantments cannot be represented by schema version 1.");
        EnchantmentModel? enchantment = card.Enchantments.SingleOrDefault();
        if (enchantment is not null)
        {
            TransplantFieldRegistry.AssertExportableModel(enchantment, $"{path}.Enchantment");
            if (enchantment.Status != EnchantmentStatus.Normal ||
                enchantment.Magnitude != decimal.Truncate(enchantment.Magnitude) ||
                enchantment.Magnitude is < int.MinValue or > int.MaxValue)
                throw new TransplantMappingException($"{path}.EnchantmentAmount",
                    "Enchantment state cannot be represented by schema version 1.");
        }
        return new CardSnapshot(card.Id.ToString(), card.CurrentUpgradeLevel,
            enchantment?.Id.ToString(), enchantment is null ? null : (int)enchantment.Magnitude,
            card.FloorAddedToDeck, cardProps);
    }
    public static RunTransplantSnapshot Export(RunState run, MapPoint target)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(target);
        if (run.CurrentRoom is not null || run.Players.Count != 1)
            throw new TransplantMappingException("RunState", "Export requires one player between rooms.");
        if (!run.HasGeneratedRoomsForTransplant)
            throw new TransplantMappingException("RoomProgress",
                "Room sequences must exist before a read-only export.");
        MapPoint? mappedTarget = run.Map.GetPoint(target.coord);
        if (!ReferenceEquals(mappedTarget, target) || run.VisitedMapCoords.Contains(target.coord))
            throw new TransplantMappingException("TargetNode", "Target is not an unvisited point on this map.");
        RoomType roomType = target.PointType switch
        {
            MapPointType.Monster => RoomType.Monster,
            MapPointType.Elite => RoomType.Elite,
            MapPointType.Boss => RoomType.Boss,
            _ => throw new TransplantMappingException("TargetRoomType", "Target is not a known combat room."),
        };
        if (run.SharedRelicGrabBag is null)
            throw new TransplantMappingException("SharedRelicBag", "Shared relic bag has not been initialized.");
        if (run.WongoPointsEarned != 0 || run.FreedRepy || run.CompletedQuests.Count != 0 ||
            run.Progress.WongoPoints != 0)
            throw new TransplantMappingException("RunState.Progress",
                "Run progression outside the current schema cannot be exported.");
        var player = run.Players[0];
        if (!player.CanUseOrRemovePotions || !player.IsActiveForHooks ||
            player.Creature.Powers.Count != 0 || player.Creature.Block != 0)
            throw new TransplantMappingException("Player",
                "Unsupported between-room player state cannot be exported.");
        var deck = new List<CardSnapshot>();
        for (int index = 0; index < player.Deck.Cards.Count; index++)
        {
            deck.Add(ExportCardForTransplant(player.Deck.Cards[index], $"Player.Deck[{index}]"));
        }
        var relics = player.Relics.Select((relic, index) => new RelicSnapshot(
            relic.Id.ToString(),
            TransplantSavedProperties.Export(relic, $"Player.Relics[{index}]"))).ToArray();
        var playerSnapshot = new PlayerSnapshot(
            player.Creature.CurrentHp, player.Creature.MaxHp, player.Gold,
            player.CardRemovalsUsed, deck, relics,
            player.PotionSlots.Select(potion => potion?.Id.ToString()).ToArray(),
            player.PlayerRng.ToSerializable(), player.Odds.ToSerializable(),
            player.RelicGrabBag.ExportForTransplant())
        {
            MaxEnergy = player.MaxEnergy,
            BaseOrbSlotCount = player.Character.BaseOrbSlotCount,
        };
        return new RunTransplantSnapshot(
            RunTransplantSnapshot.CurrentSchemaVersion,
            run.Rng.StringSeed,
            player.Character.Id.ToString(),
            run.GetAscensionLevelForTransplant(),
            run.CurrentActIndex,
            run.Acts.Select(act => act.GetType().Name).ToArray(),
            run.VisitedMapCoords.Select(coord => new MapCoordDto(coord.col, coord.row)).ToArray(),
            run.TotalFloor - run.VisitedMapCoords.Count,
            new MapCoordDto(target.coord.col, target.coord.row),
            roomType.ToString(),
            playerSnapshot,
            run.Rng.ToSerializable(), run.Odds.ToSerializable(),
            run.ExportRoomProgressForTransplant(roomType),
            run.SharedRelicGrabBag.ExportForTransplant(),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal));
    }
}
