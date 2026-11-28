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
    private static readonly RunRngType[] AllRunRngTypes = Enum.GetValues<RunRngType>();
    private static readonly PlayerRngType[] AllPlayerRngTypes = Enum.GetValues<PlayerRngType>();

    internal static int IndexOfReference<T>(IReadOnlyList<T> items, T? target) where T : class
    {
        if (target is null)
        {
            return -1;
        }
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], target))
            {
                return i;
            }
        }
        return -1;
    }

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
        if (state.RunState.GameMode != Runs.GameMode.Standard)
        {
            builder.Append((int)state.RunState.GameMode);
            IReadOnlyList<RunModel> runModels = state.RunModels;
            builder.Append(runModels.Count);
            for (int i = 0; i < runModels.Count; i++)
            {
                builder.Append(runModels[i].Id);
            }
        }
        IReadOnlyList<Player> players = state.Players;
        builder.Append(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            AppendPlayer(ref builder, state, players[i]);
        }

        IReadOnlyList<Creature> allies = state.Allies;
        builder.Append(allies.Count);
        for (int i = 0; i < allies.Count; i++)
        {
            AppendCreature(ref builder, state, allies[i]);
        }

        IReadOnlyList<Creature> enemies = state.Enemies;
        builder.Append(enemies.Count);
        for (int i = 0; i < enemies.Count; i++)
        {
            AppendCreature(ref builder, state, enemies[i]);
        }

        AppendRemovedCreatures(ref builder, state);
        AppendDamageHistory(ref builder, state);
        state.SemanticHistory.AppendStateDescription(ref builder);
    }

    public static void AppendRemovedCreatures(
        ref CombatStateDescriptionBuilder builder,
        CombatState state)
    {
        IReadOnlyList<Creature> removed = state.RemovedCreatures;
        if (removed.Count == 0)
        {
            return;
        }

        builder.Append("removed-creatures");
        builder.Append(removed.Count);
        for (int i = 0; i < removed.Count; i++)
        {
            Creature creature = removed[i];
            builder.Append(creature.CombatState is not null);
            AppendCreature(ref builder, state, creature);
        }
    }

    public static void AppendDamageHistory(
        ref CombatStateDescriptionBuilder builder,
        CombatState state)
    {
        IReadOnlyList<CombatDamageHistoryEntry> entries = state.DamageHistory.Entries;
        builder.Append(entries.Count);
        if (entries.Count == 0)
        {
            return;
        }

        Dictionary<CardModel, int>? detachedSourceOrdinals = null;
        for (int i = 0; i < entries.Count; i++)
        {
            CombatDamageHistoryEntry entry = entries[i];
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
                    ref builder, state, cardSource, ref detachedSourceOrdinals);
            }

            builder.Append(entry.RoundNumber);
            builder.Append((int)entry.CurrentSide);
            entry.AppendPlayerTurnNumbers(ref builder);
        }
    }

    private static void AppendDamageHistoryCardSource(
        ref CombatStateDescriptionBuilder builder,
        CombatState state,
        CardModel cardSource,
        ref Dictionary<CardModel, int>? detachedSourceOrdinals)
    {
        int ownerIndex = cardSource.HasOwner
            ? IndexOfReference(state.Players, cardSource.Owner)
            : -1;
        if (cardSource.HasOwner && ownerIndex < 0)
        {
            throw new InvalidOperationException(
                "Damage history contains a card source owned outside the combat state.");
        }

        builder.Append(ownerIndex);
        if (cardSource.Pile is { } pile)
        {
            int pileIndex = IndexOfReference(pile.CardList, cardSource);
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
            detachedSourceOrdinals ??= new Dictionary<CardModel, int>(ReferenceEqualityComparer.Instance);
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

        int allyIndex = IndexOfReference(state.Allies, creature);
        if (allyIndex >= 0)
        {
            builder.Append(0);
            builder.Append(allyIndex);
            return;
        }

        int escapedIndex = IndexOfReference(state.EscapedCreatures, creature);
        if (escapedIndex >= 0)
        {
            // Preserve the established ally/enemy tags; escaped creatures can remain in
            // damage history after leaving the active combat lists.
            builder.Append(2);
            builder.Append(escapedIndex);
            return;
        }

        int removedIndex = IndexOfReference(state.RemovedCreatures, creature);
        if (removedIndex >= 0)
        {
            builder.Append(3);
            builder.Append(removedIndex);
            return;
        }

        int enemyIndex = IndexOfReference(state.Enemies, creature);
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
        AppendPlayerHeaderAndState(
            ref builder,
            state,
            player,
            includeDominanceMeasuredResources: true);
        AppendPile(ref builder, player.Deck, state);
        IReadOnlyList<CardPile> allPiles = player.PlayerCombatState!.AllPiles;
        for (int i = 0; i < allPiles.Count; i++)
        {
            AppendPile(ref builder, allPiles[i], state);
        }

        List<PotionModel?> potionSlots = player.PotionSlotList;
        builder.Append(potionSlots.Count);
        for (int i = 0; i < potionSlots.Count; i++)
        {
            PotionModel? potion = potionSlots[i];
            builder.Append(potion is not null);
            if (potion is not null)
            {
                builder.Append(potion.Id);
            }
        }

        List<RelicModel> relics = player.RelicList;
        builder.Append(relics.Count);
        for (int i = 0; i < relics.Count; i++)
        {
            AppendModel(ref builder, state, relics[i]);
        }
    }

    public static void AppendPlayerHeaderAndState(
        ref CombatStateDescriptionBuilder builder,
        CombatState state,
        Player player,
        bool includeDominanceMeasuredResources)
    {
        builder.Append(player.Character.Id);
        if (includeDominanceMeasuredResources)
        {
            builder.Append(player.Creature.CurrentHp);
            builder.Append(player.Creature.MaxHp);
            builder.Append(player.Creature.Block);
            builder.Append(player.Creature.CumulativeHpLost);
        }

        builder.Append(player.Gold);
        builder.Append(player.HasEventPet());
        builder.Append(player.BaseOrbSlotCount);
        AppendPlayerRng(ref builder, player.PlayerRng);
        PlayerCombatState combat = player.PlayerCombatState!;
        builder.Append(combat.TurnNumber);
        builder.Append((int)combat.Phase);
        if (includeDominanceMeasuredResources)
        {
            builder.Append(combat.Energy);
            builder.Append(combat.Stars);
        }

        builder.Append(combat.StarsGainedThisTurn);
        builder.Append(combat.SkillCardsPlayedThisTurn);
        builder.Append(combat.AttackCardsPlayedThisTurn);
        builder.Append(combat.ShivsPlayedThisTurn);
        builder.Append(combat.CardsDiscardedThisTurn);
        builder.Append(combat.CardsPlayedThisCombat);
        builder.Append(combat.CardsGeneratedThisCombat);
        builder.Append(combat.SadnessGeneratedThisCombat);
        builder.Append(combat.CardsDrawnThisCombat);
        builder.Append(combat.CardsPlayedThisTurn);
        builder.Append(combat.ManualCardsPlayedThisTurn);
        builder.Append(combat.CardPlaysStartedThisTurn);
        builder.Append(combat.AttackOrSkillCardPlaysStartedThisTurn);
        builder.Append(combat.AttackCardsStartedThisTurn);
        builder.Append(combat.ZeroCostAttacksStartedThisTurn);
        builder.Append(combat.FirstInSeriesCardPlaysStartedThisTurn);
        builder.Append(combat.WahStartedThisTurn);
        builder.Append(combat.ChoStartedThisTurn);
        builder.Append(combat.KiStartedThisTurn);
        builder.Append(combat.FirstWahPlayOrdinalThisTurn);
        builder.Append(combat.FirstChoPlayOrdinalThisTurn);
        builder.Append(combat.FirstKiPlayOrdinalThisTurn);
        IReadOnlyList<Entities.FoolArrows.ArrowSlot> arrows = combat.Arrows;
        builder.Append(arrows.Count);
        for (int i = 0; i < arrows.Count; i++)
        {
            builder.Append((int)arrows[i].Status);
        }
        IReadOnlyList<Creature> pets = combat.Pets;
        builder.Append(pets.Count);
        for (int i = 0; i < pets.Count; i++)
        {
            AppendCreatureReference(ref builder, state, pets[i]);
        }
        builder.Append(combat.OrbQueue.Capacity);
        List<OrbModel> orbs = combat.OrbQueue.OrbList;
        builder.Append(orbs.Count);
        for (int i = 0; i < orbs.Count; i++)
        {
            OrbModel orb = orbs[i];
            builder.Append(orb.Id);
            builder.Append(orb.HasBeenRemovedFromState);
            int ownerIndex = IndexOfReference(state.Players, orb.Owner);
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
            : IndexOfReference(state.Players, creature.PetOwner));
        builder.Append(creature.IsPrimaryEnemy);
        builder.Append(creature.IsSecondaryEnemy);
        builder.Append(creature.IsAttacking);
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
                var stateLog = moveStateMachine.StateLog;
                builder.Append(stateLog.Count);
                for (int i = 0; i < stateLog.Count; i++)
                {
                    builder.Append(stateLog[i].Id);
                }
            }

            AppendRng(ref builder, monster.Rng);
            AppendModel(ref builder, state, monster);
        }
        else
        {
            builder.Append(ModelId.none);
        }

        List<PowerModel> powers = creature.PowerList;
        builder.Append(powers.Count);
        for (int i = 0; i < powers.Count; i++)
        {
            PowerModel power = powers[i];
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
        List<CardModel> cards = pile.CardList;
        builder.Append(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            AppendCard(ref builder, cards[i], state);
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
        builder.Append(card.CapturedEnergyXValue);
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
                int ownerIndex = state is null ? -1 : IndexOfReference(state.Players, deckVersion.Owner);
                int deckIndex = ownerIndex < 0
                    ? -1
                    : IndexOfReference(state!.Players[ownerIndex].Deck.CardList, deckVersion);
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

        AppendSortedKeywords(ref builder, card.Keywords);

        if (card.EnchantmentList is { Count: > 0 } enchantments)
        {
            builder.Append(enchantments.Count);
            for (int i = 0; i < enchantments.Count; i++)
            {
                EnchantmentModel enchantment = enchantments[i];
                builder.Append(enchantment.Id);
                builder.Append((int)enchantment.Status);
                builder.Append(enchantment.Magnitude);
                if (state is not null) AppendModel(ref builder, state, enchantment);
            }
        }
        else
        {
            builder.Append(0);
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

    private static void AppendSortedKeywords(
        ref CombatStateDescriptionBuilder builder,
        IReadOnlyCollection<CardKeyword> keywords)
    {
        int count = keywords.Count;
        builder.Append(count);
        if (count == 0)
        {
            return;
        }

        Span<int> buffer = count <= 16 ? stackalloc int[count] : new int[count];
        int index = 0;
        if (keywords is HashSet<CardKeyword> set)
        {
            foreach (CardKeyword kw in set)
            {
                buffer[index++] = (int)kw;
            }
        }
        else if (keywords is IReadOnlyList<CardKeyword> list)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[index++] = (int)list[i];
            }
        }
        else
        {
            foreach (CardKeyword kw in keywords)
            {
                buffer[index++] = (int)kw;
            }
        }

        if (count > 1)
        {
            buffer.Sort();
        }

        for (int i = 0; i < count; i++)
        {
            builder.Append(buffer[i]);
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
            case RelicModel relic:
                builder.Append(relic.IsWax);
                builder.Append(relic.IsMelted);
                break;
        }
        if (model is ICombatStateDescriptionContributor contributor)
        {
            contributor.AppendCombatStateDescription(ref builder, context);
        }
    }

    public static void AppendRunRng(ref CombatStateDescriptionBuilder builder, RunRngSet rngSet)
    {
        for (int i = 0; i < AllRunRngTypes.Length; i++)
        {
            RunRngType type = AllRunRngTypes[i];
            builder.Append((int)type);
            AppendRng(ref builder, rngSet.GetRng(type));
        }
    }

    public static void AppendPlayerRng(ref CombatStateDescriptionBuilder builder, PlayerRngSet rngSet)
    {
        builder.Append(rngSet.Seed);
        for (int i = 0; i < AllPlayerRngTypes.Length; i++)
        {
            PlayerRngType type = AllPlayerRngTypes[i];
            builder.Append((int)type);
            AppendRng(ref builder, rngSet.GetRng(type));
        }
    }

    private static void AppendRng(ref CombatStateDescriptionBuilder builder, Rng rng) =>
        rng.AppendStateTo(ref builder);

    public static PocketwatchStateDescription GetPocketwatchState(Pocketwatch relic) => new(
        relic.CardsPlayedThisTurn, relic.CardsPlayedPreviousTurn, relic.ShouldDrawExtra,
        relic.CanStillTriggerThisTurn, relic.CardPlayThresholdSnapshot);

    public static int GetPendingCardRewards(TheHunt card) => card.PendingCardRewardsForCombatSearch;

    public static bool CanArtOfWarTriggerNextTurn(ArtOfWar relic) => relic.CanTriggerNextTurn;

    public static decimal GetModifiedHandDraw(CombatState state, Player player) =>
        CombatEngine.GetModifiedHandDraw(state, player);
}
