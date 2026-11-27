using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>Minimal combat damage ledger used by history-dependent card formulas.</summary>
public sealed class CombatDamageHistory
{
    private readonly List<CombatDamageHistoryEntry> _entries;

    public CombatDamageHistory()
    {
        _entries = new List<CombatDamageHistoryEntry>();
    }

    private CombatDamageHistory(int capacity)
    {
        _entries = capacity > 0 ? new List<CombatDamageHistoryEntry>(capacity) : new List<CombatDamageHistoryEntry>();
    }

    internal static CombatDamageHistory Empty { get; } = new();

    public IReadOnlyList<CombatDamageHistoryEntry> Entries => _entries;

    internal void Record(
        DamageResult result,
        Creature receiver,
        Creature? dealer,
        CardModel? cardSource,
        ICombatState combatState)
    {
        if (combatState.IsLiveCombat() && !combatState.IsEnding())
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
        int count = _entries.Count;
        if (count == 0)
        {
            return new CombatDamageHistory();
        }

        var clone = new CombatDamageHistory(count);
        List<CardModel>? newlyAddedSources = null;
        for (int i = 0; i < count; i++)
        {
            clone._entries.Add(_entries[i].Clone(creatureMap, cardMap, ref newlyAddedSources));
        }

        if (newlyAddedSources is null)
        {
            return clone;
        }

        // History can introduce detached sources after CombatState cloned the ordinary piles.
        // Complete their provenance graph before any new card restores its references.
        for (int i = 0; i < newlyAddedSources.Count; i++)
        {
            CardModel card = newlyAddedSources[i];
            if (card.CloneOf is not { } origin || cardMap.ContainsKey(origin)) continue;
            if (!creatureMap.TryGetValue(origin.Owner.Creature, out Creature? clonedOwner) ||
                clonedOwner.Player is not { } player)
            {
                throw new InvalidOperationException("Detached clone origin owner was not rebound to a player.");
            }
            cardMap.Add(origin, origin.CloneForCombat(player));
            newlyAddedSources.Add(origin);
        }

        for (int i = 0; i < newlyAddedSources.Count; i++)
        {
            CardModel source = newlyAddedSources[i];
            cardMap[source].RestoreCombatCloneReferencesFrom(source, cardMap);
        }

        return clone;
    }
}
