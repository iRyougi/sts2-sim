using System.Globalization;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Reporting;

public static class SnapshotFactory
{
    public static PlayerSnapshot SnapshotPlayer(Player player) => new(
        Hp: player.Creature.CurrentHp,
        MaxHp: player.Creature.MaxHp,
        Block: player.Creature.Block,
        Energy: player.PlayerCombatState?.Energy ?? 0,
        Stars: player.PlayerCombatState?.Stars ?? 0,
        Hand: player.PlayerCombatState?.Hand.Cards.Select(CardId).ToList() ?? [],
        DrawPileSize: player.PlayerCombatState?.DrawPile.Cards.Count ?? 0,
        DiscardPileSize: player.PlayerCombatState?.DiscardPile.Cards.Count ?? 0,
        ExhaustPileSize: player.PlayerCombatState?.ExhaustPile.Cards.Count ?? 0,
        Powers: player.Creature.Powers.Select(SnapshotPower).ToList());

    public static EnemySnapshot SnapshotEnemy(Creature creature) => new(
        Slot: CombatSlot(creature) ?? string.Empty,
        Id: creature.Monster?.Id.ToString() ?? "player",
        Hp: creature.CurrentHp,
        MaxHp: creature.MaxHp,
        Block: creature.Block,
        Powers: creature.Powers.Select(SnapshotPower).ToList(),
        Intent: DescribeIntent(creature));

    public static List<string> SnapshotDeck(Player player) =>
        player.Deck.Cards.Select(CardId).ToList();

    public static List<string> SnapshotRelics(Player player) =>
        player.Relics.Select(relic => relic.Id.ToString()).ToList();

    public static List<string?> SnapshotPotions(Player player) =>
        player.PotionSlots.Select(potion => potion?.Id.ToString()).ToList();

    internal static string? CombatSlot(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        if (!string.IsNullOrWhiteSpace(creature.SlotName))
        {
            return creature.SlotName;
        }

        return creature.Monster is not null && creature.CombatId is { } combatId
            ? $"combat:{combatId.ToString(CultureInfo.InvariantCulture)}"
            : null;
    }

    private static string CardId(CardModel card) => card.Id.ToString();

    private static PowerSnapshot SnapshotPower(PowerModel power) =>
        new(power.Id.ToString(), power.Amount);

    private static string DescribeIntent(Creature creature)
    {
        MonsterModel? monster = creature.Monster;
        AbstractIntent? intent = monster?.NextMove?.Intents.FirstOrDefault();
        return intent switch
        {
            AttackIntent attack => $"{attack.IntentType} {attack.GetTotalDamage(creature.CombatState?.Allies ?? [], monster!.Creature)}",
            null => "None",
            _ => intent.IntentType.ToString(),
        };
    }
}
