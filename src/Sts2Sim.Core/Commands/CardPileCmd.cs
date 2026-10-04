using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Commands;

/// <summary>The result of a semantic pile move; an ended combat can return an unexecuted successful preflight.</summary>
public readonly record struct CardPileAddResult(CardModel Card, CardPile? OldPile, PileType TargetPile, bool Success);

/// <summary>牌堆间的移动/洗牌/抽牌命令。逐字移植调用序（<c>MegaCrit.Sts2.Core.Commands.CardPileCmd</c>）。</summary>
public static class CardPileCmd
{
    public static CardPile? Get(PileType type, Player player) => type switch
    {
        PileType.Draw => player.PlayerCombatState?.DrawPile,
        PileType.Hand => player.PlayerCombatState?.Hand,
        PileType.Discard => player.PlayerCombatState?.DiscardPile,
        PileType.Exhaust => player.PlayerCombatState?.ExhaustPile,
        PileType.Play => player.PlayerCombatState?.PlayPile,
        PileType.Deck => player.Deck,
        _ => null,
    };

    public static void Add(CardModel card, PileType pileType, CardPilePosition position = CardPilePosition.Bottom)
    {
        card.Pile?.RemoveInternal(card);
        CardPile newPile = Get(pileType, card.Owner) ?? throw new InvalidOperationException($"Player has no {pileType} pile.");
        if (pileType == PileType.Hand && newPile.Cards.Count >= CardPile.MaxCardsInHand)
        {
            newPile = Get(PileType.Discard, card.Owner)
                ?? throw new InvalidOperationException("Player has no discard pile.");
        }

        if (newPile.Type == PileType.Deck) card.FloorAddedToDeck = card.Owner.RunState.TotalFloor;
        newPile.AddInternal(card, GetInsertionIndex(card, newPile, position));
    }

    /// <summary>Moves an existing combat card and awaits its pile-change notification.
    /// New cards still enter through EnterCombat; persistent cards use AddToDeck.</summary>
    public static async Task<CardPileAddResult> AddAsync(
        ICombatState combatState, CardModel card, PileType pileType,
        CardPilePosition position = CardPilePosition.Bottom) =>
        (await AddAsync(combatState, new[] { card }, pileType, position))[0];

    /// <summary>Native Add(IEnumerable): validate first, move the whole batch, then notify in result order.
    /// This entry is limited to existing combat cards: a card with no combat pile has left this surface.</summary>
    public static async Task<IReadOnlyList<CardPileAddResult>> AddAsync(
        ICombatState combatState, IEnumerable<CardModel> cards, PileType pileType,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(cards);
        CardModel[] input = cards.ToArray();
        if (input.Length == 0) return Array.Empty<CardPileAddResult>();
        if (pileType is not (PileType.Draw or PileType.Hand or PileType.Discard or PileType.Exhaust or PileType.Play))
            throw new ArgumentOutOfRangeException(nameof(pileType), "Use AddToDeck for persistent cards.");
        if (combatState.IsEnding())
            return input.Select(card => new CardPileAddResult(card, null, PileType.None, false)).ToArray();

        var results = new List<CardPileAddResult>(input.Length);
        Player? owner = null;
        foreach (CardModel card in input)
        {
            ArgumentNullException.ThrowIfNull(card);
            Player cardOwner = card.Owner ?? throw new InvalidOperationException("Card has no owner.");
            CardPile? oldPile = card.Pile;
            bool success = oldPile is not null && oldPile.Type is not (PileType.None or PileType.Deck) &&
                !cardOwner.Creature.IsDead && cardOwner.Creature.CombatState is not null;
            results.Add(new CardPileAddResult(card, oldPile, pileType, success));
            if (!success) continue;
            if (!ReferenceEquals(cardOwner.Creature.CombatState, combatState) ||
                !combatState.ContainsCreature(cardOwner.Creature))
                throw new InvalidOperationException("Moved cards must belong to the supplied combat.");
            owner ??= cardOwner;
            if (!ReferenceEquals(owner, cardOwner))
                throw new InvalidOperationException("Cards in a pile batch must have the same owner.");
        }

        // The native ended guard is after preflight; it is distinct from the earlier IsEnding failure.
        if (!combatState.IsLiveCombat()) return results;
        foreach (CardPileAddResult result in results)
        {
            if (!result.Success) continue;
            CardModel card = result.Card;
            CardPile destination = Get(pileType, card.Owner)
                ?? throw new InvalidOperationException($"Player has no {pileType} pile.");
            if (destination.Type == PileType.Hand && destination.Cards.Count >= CardPile.MaxCardsInHand)
                destination = Get(PileType.Discard, card.Owner)!;
            card.Pile?.RemoveInternal(card);
            destination.AddInternal(card, GetInsertionIndex(card, destination, position));
        }

        foreach (CardPileAddResult result in results)
        {
            if (result.Success && (result.OldPile is null || result.OldPile.Type != result.Card.Pile?.Type))
                await Hook.AfterCardChangedPiles(result.Card.Owner.RunState, result.Card.CombatState,
                    result.Card, result.OldPile?.Type ?? PileType.None, null);
        }
        return results;
    }

    private static int GetInsertionIndex(CardModel card, CardPile pile, CardPilePosition position) => position switch
    {
        CardPilePosition.Top => 0,
        CardPilePosition.Random => card.Owner.RunState.Rng.Shuffle.NextInt(pile.Cards.Count + 1),
        _ => -1,
    };

    /// <summary>Adds a newly created card to a live combat for the first time and emits its entry hook once.</summary>
    public static Task EnterCombat(
        ICombatState combatState,
        CardModel card,
        PileType pileType,
        CardPilePosition position = CardPilePosition.Bottom) =>
        EnterCombatInternal(combatState, card, pileType, position, beforeEntryHook: null, rollbackBeforeEntryHook: null);

    /// <summary>Inserts a transformed card at its original index before entry listeners observe it.</summary>
    internal static Task EnterCombatAtIndex(ICombatState combatState, CardModel card, PileType pileType, int index) =>
        EnterCombatInternal(combatState, card, pileType, CardPilePosition.Bottom,
            beforeEntryHook: null, rollbackBeforeEntryHook: null, insertionIndex: index);

    private static async Task EnterCombatInternal(
        ICombatState combatState,
        CardModel card,
        PileType pileType,
        CardPilePosition position,
        Action? beforeEntryHook,
        Action? rollbackBeforeEntryHook,
        int? insertionIndex = null)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(card);
        Player owner = card.Owner;
        if (owner is null ||
            card.Pile is not null ||
            !combatState.IsLiveCombat() ||
            owner.Creature.CombatState != combatState ||
            !combatState.ContainsCreature(owner.Creature))
        {
            throw new InvalidOperationException("New combat cards require an owner in the active combat and no existing pile.");
        }

        if (pileType is not (PileType.Draw or PileType.Hand or PileType.Discard or PileType.Exhaust or PileType.Play))
        {
            throw new ArgumentOutOfRangeException(nameof(pileType), "New combat cards must enter a combat pile.");
        }

        // 原版 CardPileCmd.Add 在战斗结束中（例如刚打死最后一个敌人）对战斗牌堆直接返回失败：牌不入堆，
        // Random 位置不抽 Rng.Shuffle，也不派发入场钩子。生成记录与生成钩子在原版 Add 之外，照常进行。
        if (combatState.IsEnding())
        {
            beforeEntryHook?.Invoke();
            return;
        }

        CardPile destination = Get(pileType, owner)
            ?? throw new InvalidOperationException($"Player has no {pileType} pile.");
        if (pileType == PileType.Hand && destination.Cards.Count >= CardPile.MaxCardsInHand)
        {
            destination = Get(PileType.Discard, owner)
                ?? throw new InvalidOperationException("Player has no discard pile.");
        }

        AbstractModel[] listeners = Array.Empty<AbstractModel>();
        List<AbstractModel> invokedListeners = new();
        bool beforeEntryApplied = false;
        try
        {
            destination.AddInternal(card, insertionIndex ?? GetInsertionIndex(card, destination, position));
            listeners = Hook.SnapshotCardEnteredCombatListeners(combatState);
            beforeEntryHook?.Invoke();
            beforeEntryApplied = beforeEntryHook is not null;
            await Hook.AfterCardEnteredCombat(listeners, card, invokedListeners);
        }
        catch (Exception entryException)
        {
            card.Pile?.RemoveInternal(card);
            if (beforeEntryApplied)
            {
                rollbackBeforeEntryHook?.Invoke();
            }

            try
            {
                await Hook.AfterCardEntryAborted(invokedListeners, card);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Combat card entry and its listener compensation both failed.",
                    entryException,
                    rollbackException);
            }

            throw;
        }
    }

    /// <summary>Moves an existing combat card to the exhaust pile and emits the semantic exhaust hook.
    /// Generating a card directly into the exhaust pile is not an exhaust event.</summary>
    public static async Task<CardPileAddResult?> Exhaust(
        ICombatState combatState,
        CardModel card,
        bool causedByEthereal = false)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(card);
        if (combatState.IsOverOrEnding())
        {
            return null;
        }
        if (!combatState.IsLiveCombat() ||
            card.Owner.Creature.CombatState != combatState ||
            !combatState.ContainsCreature(card.Owner.Creature))
        {
            throw new InvalidOperationException("Exhausted cards require their owner to be in the active combat.");
        }

        bool departedHand = card.Pile?.Type == PileType.Hand;
        CardPileAddResult result = await AddAsync(combatState, card, PileType.Exhaust);
        if (combatState is CombatState concreteState)
            concreteState.SemanticHistory.RecordExhaust(concreteState, card.Owner);
        await Hook.AfterCardExhausted(combatState, card, causedByEthereal);
        if (!causedByEthereal)
        {
            await NotifyHandDeparture(combatState, card.Owner, departedHand);
        }
        return result;
    }

    /// <summary>Completes the semantic hand-departure boundary after the action's own hooks/effects have run.
    /// End-turn discard does not call this boundary; Ethereal exhaust explicitly suppresses it.</summary>
    internal static Task NotifyHandDeparture(ICombatState combatState, Player player, bool departedHand) =>
        departedHand ? CheckForEmptyHand(combatState, player) : Task.CompletedTask;

    internal static async Task CheckForEmptyHand(ICombatState combatState, Player player)
    {
        if (combatState.IsLiveCombat() && ReferenceEquals(player.Creature.CombatState, combatState) &&
            player.PlayerCombatState is { CardOrPotionEffectDepth: 0 } state &&
            state.Hand.Cards.Count == 0 && player.Creature.IsAlive)
            await Hook.AfterHandEmptied(combatState, player);
    }
    /// <summary>Adds a permanently acquired card to the deck and notifies run-level listeners.</summary>
    public static async Task<CardModel> AddToDeck(CardModel card, AbstractModel? clonedBy = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        PileType oldPileType = card.Pile?.Type ?? PileType.None;
        if (oldPileType == PileType.Deck)
        {
            Add(card, PileType.Deck);
            return card;
        }

        // As in CardPileCmd.Add, only a newly entering card (not a move from another pile)
        // passes through the permanent-acquisition replacement hook.
        if (oldPileType == PileType.None)
        {
            card = Hook.ModifyCardBeingAddedToDeck(card.Owner.RunState, card);
        }
        Add(card, PileType.Deck);

        // 偏离 #110：只在永久获取入口分发牌堆变化，不把所有同步战斗牌堆移动改造成异步 hook。
        await Hook.AfterCardChangedPiles(card.Owner.RunState, card, oldPileType, clonedBy);
        return card;
    }

    /// <summary>
    /// Adds a batch to the persistent deck. As in the game's enumerable Add overload, every
    /// original card enters the deck before any pile-change listener runs.
    /// </summary>
    public static async Task<IReadOnlyList<CardModel>> AddToDeck(
        IEnumerable<CardModel> cards, AbstractModel? clonedBy = null)
    {
        ArgumentNullException.ThrowIfNull(cards);
        CardModel[] input = cards.ToArray();
        if (input.Length == 0)
            return Array.Empty<CardModel>();

        foreach (CardModel card in input)
            ArgumentNullException.ThrowIfNull(card);
        Player owner = input[0].Owner;
        if (input.Any(card => !ReferenceEquals(card.Owner, owner)))
            throw new InvalidOperationException("Cards in a deck batch must have the same owner.");

        var added = new List<(CardModel Card, PileType OldPileType)>(input.Length);
        foreach (CardModel original in input)
        {
            PileType oldPileType = original.Pile?.Type ?? PileType.None;
            CardModel card = oldPileType == PileType.None
                ? Hook.ModifyCardBeingAddedToDeck(owner.RunState, original)
                : original;
            Add(card, PileType.Deck);
            added.Add((card, oldPileType));
        }

        foreach ((CardModel card, PileType oldPileType) in added)
        {
            if (oldPileType != card.Pile?.Type)
                await Hook.AfterCardChangedPiles(owner.RunState, card, oldPileType, clonedBy);
        }

        return added.Select(item => item.Card).ToArray();
    }

    /// <summary>Removes a card from its owner's persistent deck.</summary>
    public static async Task RemoveFromDeck(Player owner, CardModel card)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(card);
        if (!ReferenceEquals(card.Owner, owner) ||
            !ReferenceEquals(card.Pile, owner.Deck) ||
            !owner.Deck.Cards.Any(candidate => ReferenceEquals(candidate, card)))
        {
            throw new InvalidOperationException("Card must belong to the player and be in their persistent deck.");
        }

        if (!card.IsRemovable)
            throw new InvalidOperationException("Eternal cards cannot be removed from the persistent deck.");
        await Hook.BeforeCardRemoved(owner.RunState, card);
        Remove(card);
    }

    /// <summary>
    /// Removes a persistent deck card for Thieving Hopper's theft. Unlike ordinary removal,
    /// the source game permits this effect to take Eternal cards temporarily.
    /// </summary>
    internal static async Task RemoveFromDeckForTheft(Player owner, CardModel card)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(card);
        if (!ReferenceEquals(card.Owner, owner) ||
            !ReferenceEquals(card.Pile, owner.Deck) ||
            !owner.Deck.Cards.Any(candidate => ReferenceEquals(candidate, card)))
        {
            throw new InvalidOperationException("Card must belong to the player and be in their persistent deck.");
        }

        await Hook.BeforeCardRemoved(owner.RunState, card);
        card.Pile.RemoveInternal(card);
    }

    /// <summary>Clones canonical curse models and permanently adds the owned copies to the deck.</summary>
    public static async Task<IReadOnlyList<CardModel>> AddCursesToDeck(
        IEnumerable<CardModel> curses,
        Player owner)
    {
        ArgumentNullException.ThrowIfNull(curses);
        ArgumentNullException.ThrowIfNull(owner);
        List<CardModel> canonicalCurses = curses.ToList();
        if (canonicalCurses.Any(canonical =>
                canonical is null || !canonical.IsCanonical || canonical.Type != CardType.Curse))
        {
            throw new ArgumentException("Curses must be canonical curse models.", nameof(curses));
        }

        var added = new List<CardModel>();
        foreach (CardModel canonical in canonicalCurses)
        {
            var copy = (CardModel)canonical.MutableClone();
            copy.AssignOwner(owner);
            added.Add(await AddToDeck(copy));
        }

        return added.AsReadOnly();
    }

    public static void Remove(CardModel card)
    {
        if (!card.IsRemovable)
        {
            throw new InvalidOperationException("Eternal cards cannot be removed from the persistent deck.");
        }

        card.Pile?.RemoveInternal(card);
    }

    /// <summary>Adds a newly created card to a live combat pile, then notifies combat listeners.</summary>
    public static async Task Generate(
        ICombatState combatState,
        CardModel card,
        PileType pileType,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        PlayerCombatState? generationState = null;
        await EnterCombatInternal(
            combatState,
            card,
            pileType,
            position,
            () =>
            {
                generationState = card.Owner.PlayerCombatState
                    ?? throw new InvalidOperationException("Generated cards require player combat state.");
                generationState.RecordCardGenerated();
            },
            () => generationState!.RollbackCardGenerated());
        await Hook.AfterCardGenerated(combatState, card);
        await Hook.AfterCardGeneratedForCombat(combatState, card, card.Owner);
    }

    /// <summary>
    /// Adds a generated card while preserving the player who caused the generation.
    /// The creator may differ from the resulting card owner in multiplayer.
    /// </summary>
    public static async Task Generate(
        ICombatState combatState,
        CardModel card,
        PileType pileType,
        Player? creator,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        PlayerCombatState? generationState = null;
        await EnterCombatInternal(
            combatState,
            card,
            pileType,
            position,
            () =>
            {
                if (creator is null)
                    return;
                generationState = creator.PlayerCombatState
                    ?? throw new InvalidOperationException("Generated cards require player combat state.");
                generationState.RecordCardGenerated();
            },
            () => generationState?.RollbackCardGenerated());
        await Hook.AfterCardGenerated(combatState, card, creator);
        await Hook.AfterCardGeneratedForCombat(combatState, card, creator);
    }

    public static async Task ShuffleIfNecessary(ICombatState combatState, Player player)
    {
        PlayerCombatState state = player.PlayerCombatState!;
        if (state.DrawPile.Cards.Count == 0 && state.DiscardPile.Cards.Count > 0)
        {
            await Shuffle(combatState, player);
        }
    }

    /// <summary>弃牌堆 + 抽牌堆余牌一起重洗回抽牌堆。先稳定排序再洗牌；开局仍使用不稳定洗牌。</summary>
    public static async Task Shuffle(ICombatState combatState, Player player)
    {
        // 原版 CardPileCmd.Shuffle 开头：战斗已结束或正在结束时不洗牌，也就不消耗 Shuffle 流。
        if (combatState.IsOverOrEnding())
        {
            return;
        }

        PlayerCombatState state = player.PlayerCombatState!;
        List<CardModel> combined = state.DiscardPile.Cards.Concat(state.DrawPile.Cards).ToList();
        Rng shuffleRng = combatState is CombatState concreteState
            ? concreteState.NextShuffleRng()
            : combatState.RunState.Rng.Shuffle;
        combined.StableShuffle(shuffleRng);
        foreach (EnchantmentModel enchantment in combined.SelectMany(card => card.Enchantments).ToList())
        {
            enchantment.ModifyShuffleOrder(player, combined, isInitialShuffle: false);
        }
        foreach (CardModel card in state.DiscardPile.Cards.ToList())
        {
            state.DiscardPile.RemoveInternal(card);
        }
        foreach (CardModel card in state.DrawPile.Cards.ToList())
        {
            state.DrawPile.RemoveInternal(card);
        }
        foreach (CardModel card in combined)
        {
            state.DrawPile.AddInternal(card);
        }

        await Hook.AfterShuffle(combatState, player);
    }

    /// <summary>逐张抽牌,抽前按需重洗;手牌达上限即停，并返回本次实际抽到的牌。</summary>
    public static async Task<IReadOnlyList<CardModel>> Draw(
        ICombatState combatState,
        int count,
        Player player,
        bool fromHandDraw)
    {
        // 原版 CardPileCmd.DrawInternal 开头：战斗已结束或正在结束（例如这张牌刚打死最后一个敌人）时不抽牌。
        if (combatState.IsOverOrEnding())
        {
            return Array.Empty<CardModel>();
        }

        if (!Hook.ShouldDraw(combatState, player, fromHandDraw, out _))
        {
            return Array.Empty<CardModel>();
        }

        PlayerCombatState state = player.PlayerCombatState!;
        var drawnCards = new List<CardModel>();
        for (int i = 0; i < count; i++)
        {
            if (state.Hand.Cards.Count >= CardPile.MaxCardsInHand)
            {
                break;
            }
            if (combatState.IsOverOrEnding())
            {
                break;
            }
            await ShuffleIfNecessary(combatState, player);
            CardModel? card = state.DrawPile.Cards.FirstOrDefault();
            if (card == null)
            {
                break;
            }
            state.DrawPile.RemoveInternal(card);
            state.Hand.AddInternal(card);
            state.RecordCardDrawn();
            if (combatState is CombatState concreteState && concreteState.IsLiveCombat())
                concreteState.SemanticHistory.RecordCardDrawn(concreteState, card, fromHandDraw);
            drawnCards.Add(card);
            (combatState as CombatState)?.Observer?.CardDrawn(card);
            // Native Hook.AfterCardDrawn runs the complete Early pass before the ordinary pass.
            await Hook.AfterCardDrawn(combatState, card, fromHandDraw);
        }

        return drawnCards.AsReadOnly();
    }
}
