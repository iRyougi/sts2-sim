using System.Globalization;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Reporting;

internal sealed class NonCombatPlayerSnapshot
{
    private readonly int _hp;
    private readonly int _maxHp;
    private readonly int _gold;
    private readonly IReadOnlyList<string> _deck;
    private readonly IReadOnlyList<string> _relics;
    private readonly IReadOnlyList<string> _potions;

    private NonCombatPlayerSnapshot(Player player)
    {
        _hp = player.Creature.CurrentHp;
        _maxHp = player.Creature.MaxHp;
        _gold = player.Gold;
        _deck = SnapshotDeck(player);
        _relics = SnapshotRelics(player);
        _potions = SnapshotPotions(player);
    }

    public static NonCombatPlayerSnapshot Capture(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new NonCombatPlayerSnapshot(player);
    }

    public string DescribeChangesTo(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var changes = new List<string>();
        AddScalarChange(changes, "hp", _hp, player.Creature.CurrentHp);
        AddScalarChange(changes, "max_hp", _maxHp, player.Creature.MaxHp);
        AddScalarChange(changes, "gold", _gold, player.Gold);
        AddCollectionChanges(changes, "deck", _deck, SnapshotDeck(player));
        AddCollectionChanges(changes, "relics", _relics, SnapshotRelics(player));
        AddCollectionChanges(changes, "potions", _potions, SnapshotPotions(player));
        return changes.Count == 0 ? "none" : string.Join("; ", changes);
    }

    private static void AddScalarChange(
        ICollection<string> changes,
        string name,
        int before,
        int after)
    {
        if (before != after)
        {
            changes.Add($"{name}:{before}->{after}");
        }
    }

    private static void AddCollectionChanges(
        ICollection<string> changes,
        string name,
        IReadOnlyList<string> before,
        IReadOnlyList<string> after)
    {
        IReadOnlyList<string> removed = Difference(before, after);
        IReadOnlyList<string> added = Difference(after, before);
        if (removed.Count > 0)
        {
            changes.Add($"{name}_removed:[{string.Join(',', removed)}]");
        }
        if (added.Count > 0)
        {
            changes.Add($"{name}_added:[{string.Join(',', added)}]");
        }
    }

    private static IReadOnlyList<string> Difference(
        IReadOnlyList<string> source,
        IReadOnlyList<string> valuesToRemove)
    {
        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string value in valuesToRemove)
        {
            remaining[value] = remaining.GetValueOrDefault(value) + 1;
        }

        var result = new List<string>();
        foreach (string value in source)
        {
            int count = remaining.GetValueOrDefault(value);
            if (count > 0)
            {
                remaining[value] = count - 1;
            }
            else
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> SnapshotDeck(Player player) =>
        player.Deck.Cards.Select(DescribeCard).ToArray();

    private static string DescribeCard(CardModel card)
    {
        string id = card.Id.ToString();
        string[] enchantments = card.Enchantments
            .OrderBy(enchantment => enchantment.Id.ToString(), StringComparer.Ordinal)
            .Select(enchantment =>
                $"{enchantment.Id}:{enchantment.Magnitude.ToString(CultureInfo.InvariantCulture)}:{enchantment.Status}")
            .ToArray();
        if (card.CurrentUpgradeLevel == 0 && enchantments.Length == 0)
        {
            return id;
        }

        return $"{id}(upgrade={card.CurrentUpgradeLevel},enchantments=[{string.Join(',', enchantments)}])";
    }

    private static IReadOnlyList<string> SnapshotRelics(Player player) =>
        player.Relics
            .SelectMany(relic => Enumerable.Repeat(relic.Id.ToString(), relic.StackCount))
            .ToArray();

    private static IReadOnlyList<string> SnapshotPotions(Player player) =>
        player.PotionSlots
            .Where(potion => potion is not null)
            .Select(potion => potion!.Id.ToString())
            .ToArray();
}
