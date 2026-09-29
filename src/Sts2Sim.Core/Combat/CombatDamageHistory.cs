using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>Minimal combat damage ledger used by history-dependent card formulas.</summary>
public sealed class CombatDamageHistory
{
    private readonly List<CombatDamageHistoryEntry> _entries = new();

    internal static CombatDamageHistory Empty { get; } = new();

    public IReadOnlyList<CombatDamageHistoryEntry> Entries => _entries;

    internal void Record(
        DamageResult result,
        Creature receiver,
        Creature? dealer,
        CardModel? cardSource,
        ICombatState combatState)
    {
        if (combatState.IsLiveCombat())
        {
            _entries.Add(new CombatDamageHistoryEntry(
                result,
                receiver,
                dealer,
                cardSource,
                combatState));
        }
    }

    internal CombatDamageHistory Clone(
        IReadOnlyDictionary<Creature, Creature> creatureMap,
        Dictionary<CardModel, CardModel> cardMap)
    {
        var preexistingSources = new HashSet<CardModel>(
            cardMap.Keys,
            ReferenceEqualityComparer.Instance);
        var clone = new CombatDamageHistory();
        clone._entries.AddRange(_entries.Select(entry => entry.Clone(creatureMap, cardMap)));

        // History can introduce detached sources after CombatState cloned the ordinary piles.
        // Complete their provenance graph before any new card restores its references.
        var origins = new Queue<CardModel>(cardMap.Keys.Where(source => !preexistingSources.Contains(source)));
        while (origins.TryDequeue(out CardModel? card))
        {
            if (card.CloneOf is not { } origin || cardMap.ContainsKey(origin)) continue;
            if (!creatureMap.TryGetValue(origin.Owner.Creature, out Creature? clonedOwner) ||
                clonedOwner.Player is not { } player)
            {
                throw new InvalidOperationException("Detached clone origin owner was not rebound to a player.");
            }
            cardMap.Add(origin, origin.CloneForCombat(player));
            origins.Enqueue(origin);
        }

        foreach ((CardModel source, CardModel target) in cardMap)
        {
            if (!preexistingSources.Contains(source))
            {
                target.RestoreCombatCloneReferencesFrom(source, cardMap);
            }
        }

        return clone;
    }
}
