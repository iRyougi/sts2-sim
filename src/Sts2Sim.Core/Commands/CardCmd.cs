using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Afflictions;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Commands;

/// <summary>
/// 卡牌升级/转化命令。逐字移植调用序（<c>MegaCrit.Sts2.Core.Commands.CardCmd</c>）。
/// 偏离 #86：<c>Upgrade</c> 省略 <c>CardPreviewStyle</c> 预览高亮参数与存档 history 记录；本项目没有 UI 预览。
/// </summary>
public static class CardCmd
{
    public static Task<T?> Afflict<T>(Models.CardModel card, decimal amount)
        where T : AfflictionModel
    {
        ArgumentNullException.ThrowIfNull(card);
        var affliction = (T)Models.ModelDb.Affliction<T>().MutableClone();
        if (!affliction.CanAfflict(card))
        {
            return Task.FromResult<T?>(null);
        }

        if (card.Affliction is T existing)
        {
            existing.SetAmount(existing.Amount + amount);
            return Task.FromResult<T?>(existing);
        }

        card.AttachAffliction(affliction, amount);
        return Task.FromResult<T?>(affliction);
    }

    public static void ClearAffliction(Models.CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.ClearAfflictionInternal();
    }
    /// <summary>升级一张卡；已经满级时静默跳过。
    ///
    /// 偏离 #326（2026-09-09）：<c>CardModel.Upgrade()</c> 对不可升级的卡抛异常，那是给编程
    /// 错误用的断言，应当保留。但内容层的调用点按上游写法并不自己过滤——权威 <c>KnifeTrap</c>
    /// 会直接遍历耗尽堆里的所有 Shiv 并逐张升级，不检查可升级状态；若上游这一层也抛异常，第二次触发就会崩。
    /// 故容错放在 <c>CardCmd</c> 这一层，与上游同层。
    ///
    /// 触发路径：KnifeTrap+ 打出第二次时，耗尽堆里的 Shiv 已经是 Shiv+（满级 1/1），
    /// 再次升级直接把整局打穿。300 局不变量探针命中 1 次。</summary>
    public static void Upgrade(Models.CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.IsUpgradable)
        {
            card.Upgrade();
        }
    }

    /// <summary>Applies Retain only until this turn's hand flush has completed.</summary>
    public static void ApplySingleTurnRetain(Models.CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.ApplyTemporaryRetainThisTurn();
    }

    public static void ApplySingleTurnSly(Models.CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.ApplyTemporarySlyThisTurn();
    }

    public static Task Enchant<TEnchantment>(Models.CardModel card, decimal magnitude)
        where TEnchantment : Models.EnchantmentModel
    {
        var enchantment = (Models.EnchantmentModel)Models.ModelDb
            .Get(typeof(TEnchantment))
            .MutableClone();
        return Enchant(enchantment, card, magnitude);
    }

    public static Task Enchant(
        Models.EnchantmentModel enchantment,
        Models.CardModel card,
        decimal magnitude)
    {
        ArgumentNullException.ThrowIfNull(enchantment);
        ArgumentNullException.ThrowIfNull(card);
        if (!enchantment.CanEnchant(card))
        {
            throw new InvalidOperationException(
                $"Cannot enchant {card.Id} with {enchantment.Id}.");
        }

        Models.EnchantmentModel? existing = card.Enchantments.SingleOrDefault();
        if (existing is null)
        {
            enchantment.AssignMagnitude(magnitude);
            card.AttachEnchantment(enchantment);
            enchantment.OnAttached(card);
        }
        else
        {
            if (existing.GetType() != enchantment.GetType() ||
                !existing.IsStackable ||
                !enchantment.IsStackable)
            {
                throw new InvalidOperationException(
                    $"Cannot stack {enchantment.Id} onto {existing.Id}.");
            }

            existing.AssignMagnitude(existing.Magnitude + magnitude);
        }

        return Task.CompletedTask;
    }

    public static Task Discard(Models.CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return Discard(new[] { card });
    }

    public static async Task Discard(IEnumerable<Models.CardModel> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        Models.CardModel[] discardCards = cards.ToArray();
        if (discardCards.Length == 0)
        {
            return;
        }

        (Combat.ICombatState combatState, Player owner, bool departedHand) =
            ValidateDiscardBatch(discardCards);
        Models.CardModel[] slyCards = discardCards
            .Where(card => card.HasKeyword(CardKeyword.Sly))
            .ToArray();
        await MoveAndBroadcastDiscardCards(combatState, discardCards);
        await CardPileCmd.NotifyHandDeparture(combatState, owner, departedHand);
        await AutoPlayCmd.FromCards(combatState, owner, slyCards);
    }

    /// <summary>
    /// Discards a single owner's selected cards, then draws the requested number for that owner.
    /// An empty selection is a no-op, matching optional discard-and-redraw effects.
    /// </summary>
    public static async Task DiscardAndDraw(
        IReadOnlyList<Models.CardModel> cardsToDiscard,
        int cardsToDraw)
    {
        ArgumentNullException.ThrowIfNull(cardsToDiscard);
        ArgumentOutOfRangeException.ThrowIfNegative(cardsToDraw);
        if (cardsToDiscard.Count == 0)
        {
            return;
        }

        Models.CardModel[] discardCards = cardsToDiscard.ToArray();
        (Combat.ICombatState combatState, Player owner, bool departedHand) =
            ValidateDiscardBatch(discardCards);
        Models.CardModel[] slyCards = discardCards
            .Where(card => card.HasKeyword(CardKeyword.Sly))
            .ToArray();
        MoveDiscardCards(discardCards);

        await CardPileCmd.Draw(
            combatState,
            cardsToDraw,
            owner,
            fromHandDraw: false);
        await BroadcastDiscardHooks(combatState, discardCards);
        await CardPileCmd.NotifyHandDeparture(combatState, owner, departedHand);
        await AutoPlayCmd.FromCards(combatState, owner, slyCards);
    }

    private static (Combat.ICombatState CombatState, Player Owner, bool DepartedHand)
        ValidateDiscardBatch(IReadOnlyList<Models.CardModel> discardCards)
    {
        Models.CardModel firstCard = discardCards[0];
        ArgumentNullException.ThrowIfNull(firstCard);
        Player owner = firstCard.Owner;
        Combat.ICombatState combatState = firstCard.CombatState
            ?? throw new InvalidOperationException("Discarded cards require an active combat.");
        foreach (Models.CardModel card in discardCards)
        {
            ArgumentNullException.ThrowIfNull(card);
            if (!ReferenceEquals(card.Owner, owner) || !ReferenceEquals(card.CombatState, combatState))
            {
                throw new InvalidOperationException(
                    "All discarded cards must have the same owner and active combat.");
            }
        }

        return (
            combatState,
            owner,
            discardCards.Any(card => card.Pile?.Type == PileType.Hand));
    }

    private static async Task MoveAndBroadcastDiscardCards(
        Combat.ICombatState combatState,
        IReadOnlyList<Models.CardModel> discardCards)
    {
        foreach (Models.CardModel card in discardCards)
        {
            MoveDiscardCard(card);
            await BroadcastDiscardHook(combatState, card);
        }
    }

    private static void MoveDiscardCards(IEnumerable<Models.CardModel> discardCards)
    {
        foreach (Models.CardModel card in discardCards)
        {
            MoveDiscardCard(card);
        }
    }

    private static async Task BroadcastDiscardHooks(
        Combat.ICombatState combatState,
        IEnumerable<Models.CardModel> discardCards)
    {
        foreach (Models.CardModel card in discardCards)
        {
            await BroadcastDiscardHook(combatState, card);
        }
    }

    private static void MoveDiscardCard(Models.CardModel card)
    {
        CardPileCmd.Add(card, PileType.Discard);
        card.Owner.PlayerCombatState!.RecordCardDiscarded();
    }

    private static Task BroadcastDiscardHook(
        Combat.ICombatState combatState,
        Models.CardModel card) =>
        Hook.AfterCardDiscarded(combatState, card);

    /// <summary>把 <paramref name="original"/> 所在牌堆位置换成 <paramref name="replacement"/>（移除原卡、
    /// 在同一牌堆里加入新卡）。逐字移植核心行为（<c>MegaCrit.Sts2.Core.Commands.CardCmd.Transform</c>），
    /// 偏离 #93：省略转化动画/预览与"卡不在任何牌堆时"的多人路径——本项目单机，且所有调用点在转化前都已确认卡在牌堆里。</summary>
    public static async Task Transform(Models.CardModel original, Models.CardModel replacement)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(replacement);
        if (IsCombatEnding(original))
        {
            return;
        }
        CardPile? pile = original.Pile;
        if (pile is null)
        {
            return;
        }
        if (!original.IsTransformable)
        {
            throw new InvalidOperationException("Eternal cards cannot be transformed in the persistent deck.");
        }
        if (ReferenceEquals(original, replacement) ||
            replacement.Pile is not null ||
            replacement.CombatState is not null)
        {
            throw new InvalidOperationException("The replacement card must not already belong to a pile or combat.");
        }

        int originalIndex = pile.Cards
            .Select((card, index) => (card, index))
            .Where(item => ReferenceEquals(item.card, original))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .Single();
        if (originalIndex < 0)
        {
            throw new InvalidOperationException("The original card must be registered in its assigned pile.");
        }

        Player owner = original.Owner;
        Player? previousReplacementOwner = replacement.Owner;
        PileType pileType = pile.Type;
        Sts2Sim.Core.Combat.ICombatState? combatState = original.CombatState;
        try
        {
            replacement.AssignOwner(owner);
            if (pileType == PileType.Deck) await Hook.BeforeCardRemoved(owner.RunState, original);
            pile.RemoveInternal(original);
            if (pileType == PileType.Deck)
                replacement = Hook.ModifyCardBeingAddedToDeck(owner.RunState, replacement);
            if (pileType != PileType.Deck && combatState is not null && combatState.IsLiveCombat())
            {
                await EnterTransformedCardInCombat(combatState, replacement, pileType, originalIndex);
            }
            else
            {
                CardPileCmd.Add(replacement, pileType);
            }
            if (pileType == PileType.Deck)
            {
                await Hook.AfterCardChangedPiles(owner.RunState, replacement, pileType, null);
            }
        }
        catch
        {
            replacement.Pile?.RemoveInternal(replacement);
            replacement.AssignOwnerInternal(previousReplacementOwner);
            original.AssignOwnerInternal(owner);
            pile.AddInternal(original, originalIndex);
            throw;
        }
        if (replacement.Pile?.Type is PileType.Draw or PileType.Hand or PileType.Discard or PileType.Exhaust or PileType.Play)
        {
            await Hook.AfterCardGeneratedForCombat(replacement.CombatState!, replacement, replacement.Owner);
        }
    }

    /// <summary>便捷封装：从 <see cref="Models.ModelDb"/> 生成一张 <typeparamref name="TCard"/> 再转化。</summary>
    public static async Task<TCard> CreateAndTransform<TCard>(Models.CardModel original) where TCard : Models.CardModel
    {
        var replacement = (TCard)Models.ModelDb.Card<TCard>().MutableClone();
        await Transform(original, replacement);
        return replacement;
    }

    /// <summary>Replaces a card with a clone of the registered canonical target after validating every
    /// ownership, pile, transformability, and target precondition before the first mutation.</summary>
    public static async Task<TCard> TransformTo<TCard>(
        Models.CardModel original,
        IRunState runState)
        where TCard : Models.CardModel
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(runState);
        if (!ReferenceEquals(original.Owner.RunState, runState))
        {
            throw new InvalidOperationException("The transformed card must belong to the supplied run state.");
        }
        if (original.Pile is null)
        {
            throw new InvalidOperationException("The transformed card must belong to a card pile.");
        }
        if (!original.IsTransformable)
        {
            throw new InvalidOperationException("Eternal cards cannot be transformed in the persistent deck.");
        }
        if (!Models.ModelDb.Contains(typeof(TCard)))
        {
            throw new InvalidOperationException($"The target card {typeof(TCard).Name} must be registered.");
        }

        var replacement = (TCard)Models.ModelDb.Card<TCard>().MutableClone();
        await Transform(original, replacement);
        return replacement;
    }

    /// <summary>Transforms a card using its original pool, with quest/event/ancient/token cards
    /// redirected to colorless. Mirrors CardFactory.GetDefaultTransformationOptions.</summary>
    public static async Task<Models.CardModel> TransformToRandom(
        Models.CardModel original,
        Rng rng,
        IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(runState);
        if (!ReferenceEquals(original.Owner.RunState, runState))
        {
            throw new InvalidOperationException("The transformed card must belong to the supplied run state.");
        }
        if (original.Pile is null)
        {
            throw new InvalidOperationException("The transformed card must belong to a card pile.");
        }
        if (!original.IsTransformable)
        {
            throw new InvalidOperationException("Eternal cards cannot be transformed in the persistent deck.");
        }

        if (IsCombatEnding(original))
        {
            // Native Transform returns no results while ending; its random wrapper calls First().
            throw new InvalidOperationException("Sequence contains no elements");
        }

        Models.CardModel replacement = CardFactory.CreateRandomCardForTransform(original, original.CombatState is not null, rng);
        // Adapt the factory's owner-assigned clone to this simulator's single-card Transform
        // precondition; native Transform requires the replacement to keep the same owner.
        // Transform assigns the original owner again before inserting the replacement.
        replacement.AssignOwnerInternal(null);
        await Transform(original, replacement);
        return replacement;
    }

    /// <summary>Materializes all replacements, removes all originals, then inserts replacements in pile order.
    /// #93: presentation and the no-pile multiplayer path are omitted; the existing deck-removal
    /// notification convention is retained.</summary>
    public static async Task<IReadOnlyList<Models.CardModel>> Transform(IEnumerable<CardTransformation> transformations, Rng? rng)
    {
        CardTransformation[] items = transformations.ToArray();
        if (items.Length > 0 && IsCombatEnding(items[0].Original))
        {
            return Array.Empty<Models.CardModel>();
        }
        var prepared = new List<(Models.CardModel Original, Models.CardModel Replacement, CardPile Pile)>();
        foreach (CardTransformation item in items)
        {
            Models.CardModel original = item.Original;
            original.AssertMutable();
            CardPile pile = original.Pile ?? throw new InvalidOperationException("Transformed cards require a pile.");
            if (!original.IsTransformable) throw new InvalidOperationException("Card is not transformable.");
            Models.CardModel replacement = item.GetReplacement(rng)
                ?? throw new InvalidOperationException("Card has no transformation replacement.");
            replacement.AssertMutable();
            if (!ReferenceEquals(replacement.Owner, original.Owner) || replacement.Pile is not null)
                throw new InvalidOperationException("Replacement must belong to the original owner and no pile.");
            if (pile.Type != PileType.Deck)
            {
                var combatState = original.Owner.Creature.CombatState;
                if (combatState is null || !combatState.IsLiveCombat() ||
                    !combatState.ContainsCreature(original.Owner.Creature))
                {
                    throw new InvalidOperationException("Combat transformation requires active combat.");
                }
            }
            if (prepared.Any(entry => ReferenceEquals(entry.Original, original) || ReferenceEquals(entry.Replacement, replacement)))
                throw new InvalidOperationException("Transformations must use distinct originals and replacements.");
            prepared.Add((original, replacement, pile));
        }
        var removed = new List<(Models.CardModel Original, Models.CardModel Replacement, CardPile Pile, int Index)>();
        foreach (var entry in prepared)
        {
            IReadOnlyList<Models.CardModel> cards = entry.Pile.Cards;
            int index = -1;
            for (int candidateIndex = 0; candidateIndex < cards.Count; candidateIndex++)
            {
                if (!ReferenceEquals(cards[candidateIndex], entry.Original)) continue;
                index = candidateIndex;
                break;
            }
            if (index < 0)
                throw new InvalidOperationException("The original card must be registered in its assigned pile.");
            if (entry.Pile.Type == PileType.Deck) await Hook.BeforeCardRemoved(entry.Original.Owner.RunState, entry.Original);
            entry.Pile.RemoveInternal(entry.Original);
            removed.Add((entry.Original, entry.Replacement, entry.Pile, index));
        }
        var results = new List<Models.CardModel>();
        var ordered = removed.OrderBy(entry => entry.Pile.Type).ThenBy(entry => entry.Index).ToArray();
        for (int resultIndex = 0; resultIndex < ordered.Length; resultIndex++)
        {
            var entry = ordered[resultIndex];
            Models.CardModel replacement = entry.Replacement;
            if (entry.Pile.Type == PileType.Deck)
            {
                replacement = Hook.ModifyCardBeingAddedToDeck(entry.Original.Owner.RunState, replacement);
                replacement.FloorAddedToDeck = entry.Original.Owner.RunState.TotalFloor;
                entry.Pile.AddInternal(replacement);
                await Hook.AfterCardChangedPiles(replacement.Owner.RunState, replacement, PileType.Deck, null);
            }
            else
            {
                try
                {
                    var combatState = entry.Original.Owner.Creature.CombatState
                        ?? throw new InvalidOperationException("Combat transformation requires active combat.");
                    await EnterTransformedCardInCombat(combatState, replacement, entry.Pile.Type, entry.Index);
                }
                catch
                {
                    // Earlier entries have completed their hooks. Restore every remaining original
                    // in reverse removal order, so a failed entry cannot silently delete cards.
                    foreach (var pending in removed.AsEnumerable().Reverse()
                        .Where(item => ordered.Skip(resultIndex).Any(unfinished => ReferenceEquals(unfinished.Original, item.Original))))
                    {
                        pending.Pile.AddInternal(pending.Original, pending.Index);
                    }
                    throw;
                }
            }
            results.Add(replacement);
        }
        foreach (Models.CardModel replacement in results)
        {
            if (replacement.Pile?.Type is PileType.Draw or PileType.Hand or PileType.Discard or PileType.Exhaust or PileType.Play)
            {
                await Hook.AfterCardGeneratedForCombat(replacement.CombatState!, replacement, replacement.Owner);
            }
        }
        return results;
    }

    private static bool IsCombatEnding(Models.CardModel original) =>
        original.CombatState is { } combatState && combatState.IsLiveCombat() && combatState.IsOverOrEnding();

    private static async Task EnterTransformedCardInCombat(
        Sts2Sim.Core.Combat.ICombatState combatState,
        Models.CardModel replacement,
        PileType pileType,
        int index)
    {
        PlayerCombatState generationState = replacement.Owner.PlayerCombatState
            ?? throw new InvalidOperationException("Combat transformations require player combat state.");
        // Native Transform records CardGenerated before AfterCardEnteredCombat observes the replacement.
        generationState.RecordCardGenerated();
        try
        {
            await CardPileCmd.EnterCombatAtIndex(combatState, replacement, pileType, index);
        }
        catch
        {
            generationState.RollbackCardGenerated();
            throw;
        }
    }
}
