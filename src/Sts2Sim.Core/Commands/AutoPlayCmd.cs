using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Commands;

/// <summary>Shared command that plays a fixed number of cards from the top of a draw pile.</summary>
public static class AutoPlayCmd
{
    public static Task FromCards(
        ICombatState combatState,
        Player player,
        IEnumerable<CardModel> cards,
        Creature? fixedTarget = null) =>
        FromCardsWithResults(combatState, player, cards, fixedTarget);

    /// <summary>Autoplays while preserving autoplay hooks and spending each card's resolved resources.</summary>
    public static Task FromCardsPayingCosts(
        ICombatState combatState,
        Player player,
        IEnumerable<CardModel> cards,
        Creature? fixedTarget = null) =>
        FromCardsWithResults(combatState, player, cards, fixedTarget, spendResources: true);

    /// <summary>Autoplays one card after Whispering Earring has spent its resources in hand.</summary>
    internal static Task<IReadOnlyList<CardPlay>> FromPrepaidCard(
        ICombatState combatState,
        Player player,
        CardModel card,
        Creature? fixedTarget,
        PrepaidXCapture xCapture)
    {
        if (!ReferenceEquals(card.Owner, player) || !ReferenceEquals(card.CombatState, combatState))
            throw new InvalidOperationException("The prepaid card must belong to the supplied player and combat.");

        if (combatState is CombatState { Engine: { } engine })
            return engine.ExecuteCardActionBoundaryAsync(
                () => FromPrepaidCardCore(combatState, player, card, fixedTarget, xCapture));

        return FromPrepaidCardCore(combatState, player, card, fixedTarget, xCapture);
    }

    private static async Task<IReadOnlyList<CardPlay>> FromPrepaidCardCore(
        ICombatState combatState,
        Player player,
        CardModel card,
        Creature? fixedTarget,
        PrepaidXCapture xCapture)
    {
        // CardCmd.AutoPlay checks this after SpendResources; an ended combat keeps the card in hand.
        if (combatState.IsOverOrEnding() || player.Creature.IsDead)
            return [];

        if (card.HasKeyword(CardKeyword.Unplayable) || !Hook.ShouldPlay(combatState, card, isAutoPlay: true))
        {
            await MovePrepaidCardToResultPileWithoutPlaying(combatState, card);
            return [];
        }

        Creature? target = fixedTarget;
        if (target is null && card.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly)
        {
            IReadOnlyList<Creature> candidates =
                CombatTargetCandidates.ForCard(combatState, card.Owner, card.TargetType);
            target = combatState.RunState.Rng.CombatTargets.NextItem(candidates);
        }
        if (target is null && card.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly)
        {
            await MovePrepaidCardToResultPileWithoutPlaying(combatState, card);
            return [];
        }

        CardPlay? play = await card.AutoPlayPrevalidatedWithResultAsync(target, xCapture);
        return play is null ? [] : [play];
    }

    private static async Task MovePrepaidCardToResultPileWithoutPlaying(ICombatState combatState, CardModel card)
    {
        CardPileCmd.Add(card, PileType.Play);
        await card.MoveToResultPileWithoutPlaying(combatState);
    }

    /// <summary>Autoplays the specified cards and returns their resolved plays.</summary>
    public static async Task<IReadOnlyList<CardPlay>> FromCardsWithResults(
        ICombatState combatState,
        Player player,
        IEnumerable<CardModel> cards,
        Creature? fixedTarget = null,
        bool spendResources = false)
    {
        ArgumentNullException.ThrowIfNull(cards);
        CardModel[] snapshot = cards.ToArray();
        if (snapshot.Distinct(ReferenceEqualityComparer.Instance).Count() != snapshot.Length)
        {
            throw new ArgumentException("Autoplay cards must not contain duplicate instances.", nameof(cards));
        }

        foreach (CardModel card in snapshot)
        {
            if (!ReferenceEquals(card.Owner, player) || !ReferenceEquals(card.CombatState, combatState))
            {
                throw new InvalidOperationException("Every autoplay card must belong to the supplied player and combat.");
            }
        }

        if (combatState is CombatState concreteState &&
            concreteState.Engine is { } engine)
        {
            return await engine.ExecuteCardActionBoundaryAsync(
                () => FromCardsCore(combatState, player, snapshot, fixedTarget, spendResources));
        }

        return await FromCardsCore(combatState, player, snapshot, fixedTarget, spendResources);
    }

    public static Task FromTopOfDrawPile(
        ICombatState combatState,
        Player player,
        int count) =>
        FromTopOfDrawPileWithResults(combatState, player, count, forceExhaust: false);

    public static Task FromTopOfDrawPile(
        ICombatState combatState,
        Player player,
        int count,
        bool forceExhaust) =>
        FromTopOfDrawPileWithResults(combatState, player, count, forceExhaust);

    /// <summary>Autoplays cards and returns their resolved plays for reporting consumers.</summary>
    public static Task<IReadOnlyList<CardPlay>> FromTopOfDrawPileWithResults(
        ICombatState combatState,
        Player player,
        int count) =>
        FromTopOfDrawPileWithResults(combatState, player, count, forceExhaust: false);

    /// <summary>Autoplays cards and returns their resolved plays for reporting consumers.</summary>
    public static async Task<IReadOnlyList<CardPlay>> FromTopOfDrawPileWithResults(
        ICombatState combatState,
        Player player,
        int count,
        bool forceExhaust)
    {
        if (combatState is CombatState concreteState &&
            concreteState.Engine is { } engine)
        {
            return await engine.ExecuteCardActionBoundaryAsync(
                () => FromTopOfDrawPileCore(combatState, player, count, forceExhaust));
        }

        return await FromTopOfDrawPileCore(combatState, player, count, forceExhaust);
    }

    private static async Task<IReadOnlyList<CardPlay>> FromTopOfDrawPileCore(
        ICombatState combatState,
        Player player,
        int count,
        bool forceExhaust)
    {
        if (combatState.IsOverOrEnding()) return [];
        var cards = new List<CardModel>(count);
        for (int i = 0; i < count; i++)
        {
            await CardPileCmd.ShuffleIfNecessary(combatState, player);
            CardModel? card = player.PlayerCombatState!.DrawPile.Cards.FirstOrDefault();
            if (card is null)
            {
                break;
            }

            cards.Add(card);
            CardPileCmd.Add(card, PileType.Play);
        }

        return await PlayCardsCore(combatState, player, cards, fixedTarget: null,
            forceExhaust: forceExhaust);
    }

    private static async Task<IReadOnlyList<CardPlay>> FromCardsCore(
        ICombatState combatState,
        Player player,
        IReadOnlyList<CardModel> cards,
        Creature? fixedTarget,
        bool spendResources)
    {
        return await PlayCardsCore(combatState, player, cards, fixedTarget, spendResources,
            moveToPlay: true);
    }

    private static async Task<IReadOnlyList<CardPlay>> PlayCardsCore(
        ICombatState combatState,
        Player player,
        IReadOnlyList<CardModel> cards,
        Creature? fixedTarget,
        bool spendResources = false,
        bool? forceExhaust = null,
        bool moveToPlay = false)
    {
        var results = new List<CardPlay>(cards.Count);

        foreach (CardModel card in cards)
        {
            if (player.Creature.IsDead)
            {
                break;
            }

            // Native CardPileCmd.AutoPlayFromDrawPile assigns this per card immediately
            // before AutoPlay, including false to clear any earlier one-shot override.
            if (forceExhaust.HasValue) card.ExhaustOnNextPlay = forceExhaust.Value;

            // Native sets the one-shot exhaust flag before CardCmd.AutoPlay refuses a terminal attempt.
            // Cards preloaded into Play stay there; later cards still receive their exhaust flag.
            if (combatState.IsOverOrEnding())
            {
                continue;
            }

            // Generic CardCmd.AutoPlay attempts reject terminal combat before entering a pile.
            // Only the draw-pile command preloads the complete batch.
            if (moveToPlay) CardPileCmd.Add(card, PileType.Play);

            if (card.HasKeyword(CardKeyword.Unplayable))
            {
                await card.MoveToResultPileWithoutPlaying(combatState);
                continue;
            }
            if (!Hook.ShouldPlay(combatState, card, isAutoPlay: true))
            {
                await card.MoveToResultPileWithoutPlaying(combatState);
                continue;
            }

            Creature? target = null;
            // Generic AnyPlayer autoplay intentionally keeps its authoritative null CardPlay target.
            // Earring is the narrow paid path that supplies an explicit, validated target.
            if (card.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly ||
                card.TargetType == TargetType.AnyPlayer && spendResources && fixedTarget is not null)
            {
                IReadOnlyList<Creature> candidates =
                    CombatTargetCandidates.ForCard(combatState, card.Owner, card.TargetType);
                target = fixedTarget is not null && candidates.Contains(fixedTarget)
                    ? fixedTarget
                    : combatState.RunState.Rng.CombatTargets.NextItem(candidates);
                if (target is null)
                {
                    await card.MoveToResultPileWithoutPlaying(combatState);
                    continue;
                }
            }
            CardPlay? result = spendResources
                ? await card.AutoPlayPayingCostsWithResultAsync(target)
                : await card.AutoPlayPrevalidatedWithResultAsync(target);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }
}
