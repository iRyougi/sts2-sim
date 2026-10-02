using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat.StateDescription;

internal readonly record struct CombatStateDescriptionContext(CombatState State, AbstractModel Model)
{
    public void AssertTransientEmpty(bool isEmpty, string fieldName)
    {
        // Ending skips native cleanup hooks, so terminal snapshots may retain command
        // state. Live and starting combat still require an empty node boundary.
        if (!isEmpty && (!State.IsOverOrEnding() || State.IsStarting()))
        {
            throw new InvalidOperationException(
                $"Combat search node boundary retained transient state {Model.GetType().Name}.{fieldName}.");
        }
    }

    public void AppendCardReferences(
        ref CombatStateDescriptionBuilder builder,
        IEnumerable<CardModel?> cards)
    {
        CombatStateDescriptionContext context = this;
        string[] locations = cards
            .Where(card => card is not null)
            .Select(card => context.FindCardLocation(card!))
            .OrderBy(location => location, StringComparer.Ordinal)
            .ToArray();
        builder.Append(locations.Length);
        foreach (string location in locations)
        {
            builder.Append(location);
        }
    }

    public void AppendCardReferenceValues(
        ref CombatStateDescriptionBuilder builder,
        IEnumerable<KeyValuePair<CardModel, int>> values)
    {
        CombatStateDescriptionContext context = this;
        var ordered = values
            .Select(entry => (Location: context.FindCardLocation(entry.Key), entry.Value))
            .OrderBy(entry => entry.Location, StringComparer.Ordinal)
            .ToArray();
        builder.Append(ordered.Length);
        foreach ((string location, int value) in ordered)
        {
            builder.Append(location);
            builder.Append(value);
        }
    }

    public void AppendCreatureReferences(
        ref CombatStateDescriptionBuilder builder,
        IEnumerable<Entities.Creatures.Creature> creatures)
    {
        CombatStateDescriptionContext context = this;
        string[] locations = creatures
            .Select(context.FindCreatureLocation)
            .OrderBy(location => location, StringComparer.Ordinal)
            .ToArray();
        builder.Append(locations.Length);
        foreach (string location in locations)
        {
            builder.Append(location);
        }
    }

    public void AppendCreatureReferenceValues(
        ref CombatStateDescriptionBuilder builder,
        IEnumerable<KeyValuePair<Entities.Creatures.Creature, decimal>> values)
    {
        CombatStateDescriptionContext context = this;
        var ordered = values
            .Select(entry => (Location: context.FindCreatureLocation(entry.Key), entry.Value))
            .OrderBy(entry => entry.Location, StringComparer.Ordinal)
            .ToArray();
        builder.Append(ordered.Length);
        foreach ((string location, decimal value) in ordered)
        {
            builder.Append(location);
            builder.Append(value);
        }
    }

    public void AppendDetachedCard(
        ref CombatStateDescriptionBuilder builder,
        CardModel card) =>
        CombatStateDescription.AppendCardState(ref builder, card, State);

    public void AppendCardCloneOrigin(ref CombatStateDescriptionBuilder builder, CardModel origin)
    {
        builder.Append(FindCardLocation(origin));
        if (origin.Pile is null)
        {
            // A removed source has no ordinary pile entry, but its mutable state remains reachable.
            builder.Append(State.Players.ToList().IndexOf(origin.Owner));
            builder.Append(origin.DeckVersion is not null);
            if (origin.DeckVersion is { } deckVersion)
            {
                AppendCardReferences(ref builder, new[] { deckVersion });
            }
            AppendDetachedCard(ref builder, origin);
        }
    }

    private string FindCardLocation(CardModel card)
    {
        for (int playerIndex = 0; playerIndex < State.Players.Count; playerIndex++)
        {
            var player = State.Players[playerIndex];
            string? deck = FindInPile(player.Deck, playerIndex, card);
            if (deck is not null)
            {
                return deck;
            }

            foreach (var pile in player.PlayerCombatState!.AllPiles)
            {
                string? location = FindInPile(pile, playerIndex, card);
                if (location is not null)
                {
                    return location;
                }
            }
        }

        // Stable graph anchors distinguish shared versus independent detached origins, even when
        // their card IDs, upgrade levels, and current values happen to be identical.
        for (int playerIndex = 0; playerIndex < State.Players.Count; playerIndex++)
        {
            var player = State.Players[playerIndex];
            IEnumerable<Entities.Cards.CardPile> piles = new[] { player.Deck }.Concat(
                player.PlayerCombatState?.AllPiles ?? Array.Empty<Entities.Cards.CardPile>());
            foreach (var pile in piles)
            {
                for (int cardIndex = 0; cardIndex < pile.Cards.Count; cardIndex++)
                {
                    int depth = 0;
                    for (CardModel? origin = pile.Cards[cardIndex].CloneOf; origin is not null; origin = origin.CloneOf)
                    {
                        if (ReferenceEquals(origin, card))
                        {
                            return $"clone-origin:{playerIndex}:{(int)pile.Type}:{cardIndex}:{depth}:{card.Id}";
                        }
                        depth++;
                    }
                }
            }
        }

        return $"unbound:{card.Id}:{card.CurrentUpgradeLevel}";
    }

    private static string? FindInPile(
        Entities.Cards.CardPile pile,
        int playerIndex,
        CardModel card)
    {
        for (int cardIndex = 0; cardIndex < pile.Cards.Count; cardIndex++)
        {
            if (ReferenceEquals(pile.Cards[cardIndex], card))
            {
                return $"{playerIndex}:{(int)pile.Type}:{cardIndex}:{card.Id}";
            }
        }

        return null;
    }

    private string FindCreatureLocation(Entities.Creatures.Creature creature)
    {
        for (int playerIndex = 0; playerIndex < State.Players.Count; playerIndex++)
        {
            if (ReferenceEquals(State.Players[playerIndex].Creature, creature))
            {
                return $"player:{playerIndex}";
            }
        }

        for (int enemyIndex = 0; enemyIndex < State.Enemies.Count; enemyIndex++)
        {
            if (ReferenceEquals(State.Enemies[enemyIndex], creature))
            {
                return $"enemy:{enemyIndex}";
            }
        }

        for (int removedIndex = 0; removedIndex < State.RemovedCreatures.Count; removedIndex++)
        {
            if (ReferenceEquals(State.RemovedCreatures[removedIndex], creature))
            {
                return $"removed:{removedIndex}";
            }
        }

        throw new InvalidOperationException(
            $"{Model.GetType().Name} tracked a creature outside the combat state.");
    }
}
