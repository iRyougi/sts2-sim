using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Saves;

using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Combat.StateDescription;

/// <summary>Accurate in-process combat descriptions; not a player-visible serialization contract.</summary>
public static class CombatStateDescription
{
    public static void AppendExactState(ref CombatStateDescriptionBuilder builder, CombatState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        builder.Append("combat-state-v2");
        builder.Append((int)state.CurrentSide);
        builder.Append(state.RoundNumber);
        builder.Append(state.IsPlayerExtraTurn);
        builder.Append(state.Engine?.IsInProgress ?? false);
        builder.Append(state.Engine?.Won ?? false);
        builder.Append(state.RunState.Rng.Seed);
        AppendRunRng(ref builder, state.RunState.Rng);
        builder.Append(state.Players.Count);
        foreach (Player player in state.Players)
        {
            AppendPlayer(ref builder, state, player);
        }

        builder.Append(state.Allies.Count);
        foreach (Creature ally in state.Allies)
        {
            AppendCreature(ref builder, state, ally);
        }

        builder.Append(state.Enemies.Count);
        foreach (Creature enemy in state.Enemies)
        {
            AppendCreature(ref builder, state, enemy);
        }

        AppendRemovedCreatures(ref builder, state);
        AppendDamageHistory(ref builder, state);
        state.SemanticHistory.AppendStateDescription(ref builder);
    }

    public static void AppendRemovedCreatures(
        ref CombatStateDescriptionBuilder builder,
        CombatState state)
    {
        if (state.RemovedCreatures.Count == 0)
        {
            return;
        }

        builder.Append("removed-creatures");
        builder.Append(state.RemovedCreatures.Count);
        foreach (Creature creature in state.RemovedCreatures)
        {
            builder.Append(creature.CombatState is not null);
            AppendCreature(ref builder, state, creature);
        }
    }

    public static void AppendDamageHistory(
        ref CombatStateDescriptionBuilder builder,
        CombatState state)
    {
        CombatDamageHistoryEntry[] entries = state.DamageHistory.Entries.ToArray();
        builder.Append(entries.Length);
        var detachedSourceOrdinals = new Dictionary<CardModel, int>(
            ReferenceEqualityComparer.Instance);

        foreach (CombatDamageHistoryEntry entry in entries)
        {
            AppendCreatureReference(ref builder, state, entry.Receiver);
            AppendCreatureReference(ref builder, state, entry.Dealer);
            builder.Append((int)entry.Result.Props);
            builder.Append(entry.Result.BlockedDamage);
            builder.Append(entry.Result.UnblockedDamage);
            builder.Append(entry.Result.OverkillDamage);
            builder.Append(entry.Result.WasFullyBlocked);
            builder.Append(entry.Result.WasTargetKilled);
            builder.Append(entry.CardSource is not null);
            if (entry.CardSource is { } cardSource)
            {
                AppendDamageHistoryCardSource(
                    ref builder, state, cardSource, detachedSourceOrdinals);
            }

            builder.Append(entry.RoundNumber);
            builder.Append((int)entry.CurrentSide);
            builder.Append(entry.PlayerTurnNumbers.Count);
            foreach (int turnNumber in entry.PlayerTurnNumbers)
            {
                builder.Append(turnNumber);
            }
        }
    }

    private static void AppendDamageHistoryCardSource(
        ref CombatStateDescriptionBuilder builder,
        CombatState state,
        CardModel cardSource,
        Dictionary<CardModel, int> detachedSourceOrdinals)
    {
        int ownerIndex = cardSource.HasOwner
            ? state.Players.ToList().IndexOf(cardSource.Owner)
            : -1;
        if (cardSource.HasOwner && ownerIndex < 0)
        {
            throw new InvalidOperationException(
                "Damage history contains a card source owned outside the combat state.");
        }

        builder.Append(ownerIndex);
        if (cardSource.Pile is { } pile)
        {
            int pileIndex = pile.Cards.ToList().IndexOf(cardSource);
            if (ownerIndex < 0 || pileIndex < 0)
            {
                throw new InvalidOperationException(
                    "Damage history card source pile relationship is inconsistent.");
            }

            builder.Append(0);
            builder.Append((int)pile.Type);
            builder.Append(pileIndex);
        }
        else
        {
            if (!detachedSourceOrdinals.TryGetValue(cardSource, out int ordinal))
            {
                ordinal = detachedSourceOrdinals.Count;
                detachedSourceOrdinals.Add(cardSource, ordinal);
            }

            builder.Append(1);
            builder.Append(ordinal);
        }

        // The source is behavior-bearing state, not just a model id: upgrade-derived
        // costs/keywords and private fingerprint contributors can change future hooks.
        AppendCardState(ref builder, cardSource, state);
    }

    private static void AppendCreatureReference(
        ref CombatStateDescriptionBuilder builder,
        CombatState state,
        Creature? creature)
    {
        if (creature is null)
        {
            builder.Append(-1);
            return;
        }

        int allyIndex = state.Allies.ToList().IndexOf(creature);
        if (allyIndex >= 0)
        {
            builder.Append(0);
            builder.Append(allyIndex);
            return;
        }

        int escapedIndex = state.EscapedCreatures.ToList().IndexOf(creature);
        if (escapedIndex >= 0)
        {
            // Preserve the established ally/enemy tags; escaped creatures can remain in
            // damage history after leaving the active combat lists.
            builder.Append(2);
            builder.Append(escapedIndex);
            return;
        }

        int removedIndex = state.RemovedCreatures.ToList().IndexOf(creature);
        if (removedIndex >= 0)
        {
            builder.Append(3);
            builder.Append(removedIndex);
            return;
        }

        int enemyIndex = state.Enemies.ToList().IndexOf(creature);
        if (enemyIndex < 0)
        {
            throw new InvalidOperationException("Damage history contains a creature outside the combat state.");
        }

        builder.Append(1);
        builder.Append(enemyIndex);
    }

    private static void AppendPlayer(
        ref CombatStateDescriptionBuilder builder,
        CombatState state,
        Player player)
    {
        builder.Append(player.Character.Id);
        builder.Append(player.Creature.CurrentHp);
        builder.Append(player.Creature.MaxHp);
        builder.Append(player.Creature.Block);
        builder.Append(player.Creature.CumulativeHpLost);
        builder.Append(player.Gold);
        builder.Append(player.HasEventPet());
        builder.Append(player.BaseOrbSlotCount);
        AppendPlayerRng(ref builder, player.PlayerRng);
        PlayerCombatState combat = player.PlayerCombatState!;
        builder.Append(combat.TurnNumber);
        builder.Append((int)combat.Phase);
        builder.Append(combat.Energy);
        builder.Append(combat.Stars);
        builder.Append(combat.StarsGainedThisTurn);
        builder.Append(combat.SkillCardsPlayedThisTurn);
        builder.Append(combat.AttackCardsPlayedThisTurn);
        builder.Append(combat.ShivsPlayedThisTurn);
        builder.Append(combat.CardsDiscardedThisTurn);
        builder.Append(combat.CardsPlayedThisCombat);
        builder.Append(combat.CardsGeneratedThisCombat);
        builder.Append(combat.CardsDrawnThisCombat);
        builder.Append(combat.CardsPlayedThisTurn);
        builder.Append(combat.ManualCardsPlayedThisTurn);
        builder.Append(combat.CardPlaysStartedThisTurn);
        builder.Append(combat.AttackOrSkillCardPlaysStartedThisTurn);
        builder.Append(combat.AttackCardsStartedThisTurn);
        builder.Append(combat.ZeroCostAttacksStartedThisTurn);
        builder.Append(combat.FirstInSeriesCardPlaysStartedThisTurn);
        builder.Append(combat.Pets.Count);
        foreach (Creature pet in combat.Pets)
        {
            AppendCreatureReference(ref builder, state, pet);
        }
        builder.Append(combat.OrbQueue.Capacity);
        builder.Append(combat.OrbQueue.Orbs.Count);
        foreach (OrbModel orb in combat.OrbQueue.Orbs)
        {
            builder.Append(orb.Id);
            builder.Append(orb.HasBeenRemovedFromState);
            int ownerIndex = state.Players.ToList().IndexOf(orb.Owner);
            if (ownerIndex < 0)
                throw new InvalidOperationException("Orb owner is outside the combat state.");
            builder.Append(ownerIndex);
            switch (orb)
            {
                case DarkOrb dark:
                    builder.Append(dark.RawEvokeValue);
                    break;
                case GlassOrb glass:
                    builder.Append(glass.RawPassiveValue);
                    break;
            }
            AppendModel(ref builder, state, orb);
        }
        AppendPile(ref builder, player.Deck, state);
        foreach (CardPile pile in combat.AllPiles)
        {
            AppendPile(ref builder, pile, state);
        }

        builder.Append(player.PotionSlots.Count);
        foreach (PotionModel? potion in player.PotionSlots)
        {
            builder.Append(potion is not null);
            if (potion is not null)
            {
                builder.Append(potion.Id);
            }
        }

        builder.Append(player.Relics.Count);
        foreach (RelicModel relic in player.Relics)
        {
            AppendModel(ref builder, state, relic);
        }
    }

    private static void AppendCreature(
        ref CombatStateDescriptionBuilder builder, CombatState state, Creature creature)
    {
        AppendCreatureIdentity(ref builder, creature);
        builder.Append(creature.CurrentHp);
        builder.Append(creature.MaxHp);
        builder.Append(creature.Block);
        builder.Append(creature.CumulativeHpLost);
        AppendCreatureState(ref builder, state, creature);
    }

    public static void AppendCreatureIdentity(ref CombatStateDescriptionBuilder builder, Creature creature)
    {
        builder.Append(creature.CombatId);
        builder.Append((int)creature.Side);
        builder.Append(creature.SlotName);
    }

    public static void AppendCreatureState(
        ref CombatStateDescriptionBuilder builder, CombatState state, Creature creature)
    {
        builder.Append(creature.PetOwner is null
            ? -1
            : state.Players.ToList().IndexOf(creature.PetOwner));
        builder.Append(creature.IsPrimaryEnemy);
        builder.Append(creature.IsSecondaryEnemy);
        if (creature.Monster is { } monster)
        {
            builder.Append(monster.Id);
            builder.Append(monster.IsPerformingMove);
            builder.Append(monster.SpawnedThisTurn);
            builder.Append(monster.NextMove?.StateId);
            builder.Append(monster.NextMove?.CanTransitionAway ?? false);
            builder.Append(monster.MoveStateMachine is not null);
            if (monster.MoveStateMachine is { } moveStateMachine)
            {
                builder.Append(moveStateMachine.CurrentState.Id);
                builder.Append(moveStateMachine.CurrentState.CanTransitionAway);
                builder.Append(moveStateMachine.PerformedFirstMove);
                builder.Append(moveStateMachine.StateLog.Count);
                foreach (var stateLogEntry in moveStateMachine.StateLog)
                {
                    builder.Append(stateLogEntry.Id);
                }
            }

            AppendRng(ref builder, monster.Rng);
            AppendModel(ref builder, state, monster);
        }
        else
        {
            builder.Append(ModelId.none);
        }

        builder.Append(creature.Powers.Count);
        foreach (PowerModel power in creature.Powers)
        {
            builder.Append(power.Id);
            builder.Append(power.Amount);
            builder.Append(power.SkipNextDurationTick);
            builder.Append(power.Applier?.CombatId);
            AppendModel(ref builder, state, power);
        }
    }

    private static void AppendPile(
        ref CombatStateDescriptionBuilder builder,
        CardPile pile,
        CombatState state)
    {
        builder.Append((int)pile.Type);
        builder.Append(pile.Cards.Count);
        foreach (CardModel card in pile.Cards)
        {
            AppendCard(ref builder, card, state);
        }
    }

    public static void AppendCard(
        ref CombatStateDescriptionBuilder builder, CardModel card, CombatState? state) =>
        AppendCardCore(ref builder, card, state, includePile: true);

    public static void AppendCardState(
        ref CombatStateDescriptionBuilder builder, CardModel card, CombatState? state) =>
        AppendCardCore(ref builder, card, state, includePile: false);

    private static void AppendCardCore(
        ref CombatStateDescriptionBuilder builder,
        CardModel card,
        CombatState? state,
        bool includePile = true)
    {
        builder.Append(card.Id);
        builder.Append((int)card.Type);
        builder.Append((int)card.Rarity);
        builder.Append((int)card.TargetType);
        builder.Append(card.CurrentUpgradeLevel);
        builder.Append(card.EnergyCost);
        builder.Append(card.StarCost);
        builder.Append(card.CostsXEnergy);
        builder.Append(card.HasStarCost);
        builder.Append(card.BaseReplayCount);
        builder.Append(card.TemporaryFreeThisTurn);
        builder.Append(card.TemporaryRetainThisTurn);
        builder.Append(card.TemporarySlyThisTurn);
        builder.Append(card.TemporaryFreeUntilPlayed);
        builder.Append(card.TemporaryFreeThisCombat);
        builder.Append(card.TemporaryCostOverrideThisTurn ?? int.MinValue);
        builder.Append(card.TemporaryCostOverrideThisTurnOrUntilPlayed ?? int.MinValue);
        builder.Append(card.TemporaryCostOverrideThisCombat ?? int.MinValue);
        builder.Append(card.FloorAddedToDeck ?? int.MinValue);
        if (includePile)
        {
            builder.Append(card.DeckVersion is not null);
            if (card.DeckVersion is { } deckVersion)
            {
                int ownerIndex = state?.Players.ToList().IndexOf(deckVersion.Owner) ?? -1;
                int deckIndex = ownerIndex < 0
                    ? -1
                    : state!.Players[ownerIndex].Deck.Cards.ToList().IndexOf(deckVersion);
                if (ownerIndex < 0 || deckIndex < 0)
                {
                    throw new InvalidOperationException(
                        "A combat card DeckVersion must belong to a player deck in the combat state.");
                }

                builder.Append(ownerIndex);
                builder.Append(deckIndex);
            }
            builder.Append((int)(card.Pile?.Type ?? PileType.None));
        }

        CardKeyword[] keywords = card.Keywords.OrderBy(keyword => keyword).ToArray();
        builder.Append(keywords.Length);
        foreach (CardKeyword keyword in keywords)
        {
            builder.Append((int)keyword);
        }

        builder.Append(card.Enchantments.Count);
        foreach (EnchantmentModel enchantment in card.Enchantments)
        {
            builder.Append(enchantment.Id);
            builder.Append((int)enchantment.Status);
            builder.Append(enchantment.Magnitude);
            if (state is not null) AppendModel(ref builder, state, enchantment);
        }

        builder.Append(card.Affliction is not null);
        if (card.Affliction is not null)
        {
            builder.Append(card.Affliction.Id);
            builder.Append(card.Affliction.Amount);
        }

        if (state is not null)
        {
            AppendModel(ref builder, state, card);
        }
    }

    public static void AppendModel(
        ref CombatStateDescriptionBuilder builder,
        CombatState state,
        AbstractModel model)
    {
        var context = new CombatStateDescriptionContext(state, model);
        // Base gameplay state is mandatory; a concrete explicit interface implementation
        // may replace its custom contributor, but cannot bypass these nonvirtual calls.
        switch (model)
        {
            case CardModel card:
                card.AppendIntrinsicCombatStateDescription(ref builder, context);
                break;
            case PowerModel power:
                power.AppendIntrinsicCombatStateDescription(ref builder, context);
                break;
        }
        if (model is ICombatStateDescriptionContributor contributor)
        {
            contributor.AppendCombatStateDescription(ref builder, context);
        }
    }

    public static void AppendRunRng(ref CombatStateDescriptionBuilder builder, RunRngSet rngSet)
    {
        foreach (RunRngType type in Enum.GetValues<RunRngType>())
        {
            builder.Append((int)type);
            AppendRng(ref builder, rngSet.GetRng(type));
        }
    }

    public static void AppendPlayerRng(ref CombatStateDescriptionBuilder builder, PlayerRngSet rngSet)
    {
        builder.Append(rngSet.Seed);
        foreach (PlayerRngType type in Enum.GetValues<PlayerRngType>())
        {
            builder.Append((int)type);
            AppendRng(ref builder, rngSet.GetRng(type));
        }
    }

    private static void AppendRng(ref CombatStateDescriptionBuilder builder, Rng rng)
    {
        SerializableRng state = rng.ToSerializable();
        builder.Append(state.counter);
        builder.Append(state.state0);
        builder.Append(state.state1);
        builder.Append(state.state2);
        builder.Append(state.state3);
    }

    public static PocketwatchStateDescription GetPocketwatchState(Pocketwatch relic) => new(
        relic.CardsPlayedThisTurn, relic.CardsPlayedPreviousTurn, relic.ShouldDrawExtra,
        relic.CanStillTriggerThisTurn, relic.CardPlayThresholdSnapshot);

    public static int GetPendingCardRewards(TheHunt card) => card.PendingCardRewardsForCombatSearch;

    public static bool CanArtOfWarTriggerNextTurn(ArtOfWar relic) => relic.CanTriggerNextTurn;

    public static decimal GetModifiedHandDraw(CombatState state, Player player) =>
        CombatEngine.GetModifiedHandDraw(state, player);
}
