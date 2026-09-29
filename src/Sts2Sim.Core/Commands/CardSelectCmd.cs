using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Commands;

/// <summary>
/// 选卡命令。偏离 #92：真实游戏这是一整套 UI/多人网络同步的玩家交互系统
/// （<c>NDeckCardSelectScreen</c>/<c>NCombatPileCardSelectScreen</c> 等，含 PlayerChoiceSynchronizer 远程同步），
/// 本项目通过 <see cref="ICardSelectionDecisionSource"/> 把真实选择交给运行时/搜索策略，并在命令边界校验
/// 数量、取消、重复和候选归属。
/// </summary>
public static class CardSelectCmd
{
    public static Task<IReadOnlyList<CardModel>> SelectCardsAsync(
        ICombatState combatState,
        Player player,
        IEnumerable<CardModel> candidates,
        int minCount,
        int maxCount,
        AbstractModel? source,
        bool cancelable = false)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        return SelectCardsAsync(
            combatState.CardSelectionSource,
            player,
            candidates,
            minCount,
            maxCount,
            source,
            cancelable);
    }

    /// <summary>Selects cards outside combat through the current run driver or autonomous policy.</summary>
    public static Task<IReadOnlyList<CardModel>> SelectCardsAsync(
        IRunState runState,
        Player player,
        IEnumerable<CardModel> candidates,
        int minCount,
        int maxCount,
        AbstractModel? source,
        bool cancelable = false)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(player);
        if (!ReferenceEquals(player.RunState, runState))
        {
            throw new InvalidOperationException("Card selection player does not belong to the supplied run state.");
        }

        return SelectCardsAsync(
            runState.CardSelectionSource,
            player,
            candidates,
            minCount,
            maxCount,
            source,
            cancelable);
    }

    private static async Task<IReadOnlyList<CardModel>> SelectCardsAsync(
        ICardSelectionDecisionSource decisionSource,
        Player player,
        IEnumerable<CardModel> candidates,
        int minCount,
        int maxCount,
        AbstractModel? source,
        bool cancelable)
    {
        ArgumentNullException.ThrowIfNull(decisionSource);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(candidates);
        if (minCount < 0 || maxCount < minCount)
        {
            throw new ArgumentOutOfRangeException(nameof(minCount));
        }

        List<CardModel> options = candidates.ToList();
        if (options.Distinct(ReferenceEqualityComparer.Instance).Count() != options.Count)
        {
            throw new InvalidOperationException("Card selection candidates must be unique instances.");
        }
        if (options.Count == 0)
        {
            return Array.Empty<CardModel>();
        }

        int effectiveMax = Math.Min(maxCount, options.Count);
        int effectiveMin = Math.Min(minCount, effectiveMax);
        if (options.Count <= minCount && !cancelable)
        {
            return options.AsReadOnly();
        }
        var request = new CardSelectionRequest(
            player,
            options.AsReadOnly(),
            effectiveMin,
            effectiveMax,
            source,
            cancelable);
        IReadOnlyList<CardModel> selected =
            await decisionSource.ChooseCardsAsync(request);
        if ((!request.Cancelable && selected.Count < effectiveMin) ||
            (request.Cancelable && selected.Count != 0 && selected.Count < effectiveMin) ||
            selected.Count > effectiveMax)
        {
            throw new InvalidOperationException(
                $"Card selection returned {selected.Count} cards; expected {effectiveMin}..{effectiveMax}.");
        }

        var optionSet = new HashSet<CardModel>(options, ReferenceEqualityComparer.Instance);
        var selectedSet = new HashSet<CardModel>(ReferenceEqualityComparer.Instance);
        foreach (CardModel card in selected)
        {
            if (!optionSet.Contains(card) || !selectedSet.Add(card))
            {
                throw new InvalidOperationException(
                    "Card selection returned a duplicate or a card outside the offered candidates.");
            }
        }

        return selected.ToList().AsReadOnly();
    }

    public static Task<IReadOnlyList<CardModel>> FromDeckForTransformation(Player player, int count, AbstractModel? source = null) =>
        SelectCardsAsync(player.RunState, player,
            player.Deck.Cards.Where(card => card.Type != CardType.Quest && card.IsTransformable),
            count, count, source);

    public static Task<IReadOnlyList<CardModel>> FromDeckForUpgrade(Player player, int count, AbstractModel? source = null) =>
        SelectCardsAsync(player.RunState, player,
            player.Deck.Cards.Where(card => card.IsUpgradable), count, count, source);

    public static Task<IReadOnlyList<CardModel>> FromDeckForRemoval(Player player, int count, AbstractModel? source = null) =>
        SelectCardsAsync(player.RunState, player,
            player.Creature.IsDead ? Array.Empty<CardModel>() :
                player.Deck.Cards.Where(card => card.IsRemovable)
                    .OrderBy(card => card.Type == CardType.Curse ? 0 : 1),
            count, count, source);

    /// <summary>Each candidate is the first card of a complete visible bundle, chosen atomically.</summary>
    public static async Task<IReadOnlyList<CardModel>> FromChooseABundleScreen(
        Player player, IReadOnlyList<IReadOnlyList<CardModel>> bundles, AbstractModel source)
    {
        if (bundles.Count == 0 || bundles.Any(bundle => bundle.Count == 0))
            throw new ArgumentException("Bundle selection requires nonempty bundles.", nameof(bundles));
        var options = bundles.Select(bundle => (IReadOnlyList<CardModel>)bundle.ToArray()).ToArray();
        var representatives = options.Select(bundle => bundle[0]).ToArray();
        var request = new CardSelectionRequest(player, representatives, 1, 1, source,
            Bundles: options);
        IReadOnlyList<CardModel> selected = await player.RunState.CardSelectionSource.ChooseCardsAsync(request);
        int index = selected.Count == 1
            ? Array.FindIndex(representatives, card => ReferenceEquals(card, selected[0])) : -1;
        if (index < 0)
            throw new InvalidOperationException("Bundle selection must return exactly one offered bundle representative.");
        return options[index];
    }

    /// <summary>Selects an exact number of cards from the player's current hand for a discard effect.</summary>
    public static Task<IReadOnlyList<CardModel>> FromHandForDiscard(
        ICombatState combatState,
        Player player,
        int count,
        AbstractModel? source)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        PlayerCombatState state = player.PlayerCombatState
            ?? throw new InvalidOperationException("Discard selection requires an active combat.");
        return FromHand(
            combatState,
            player,
            state.Hand.Cards,
            count,
            count,
            source);
    }

    /// <summary>原版 <c>CardSelectCmd.FromHand</c>：从手牌选牌。战斗已结束或正在结束（例如这张牌刚打死
    /// 最后一个敌人）时直接返回空，不产生选牌，调用方的后续效果也随之跳过。只有"从手牌选"的入口带这个守卫；
    /// 原版的三选一、网格选牌等入口没有，不能把它加到 <see cref="SelectCardsAsync(ICombatState, Player, IEnumerable{CardModel}, int, int, AbstractModel?, bool)"/> 上。</summary>
    public static Task<IReadOnlyList<CardModel>> FromHand(
        ICombatState combatState,
        Player player,
        IEnumerable<CardModel> candidates,
        int minCount,
        int maxCount,
        AbstractModel? source,
        bool cancelable = false)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        if (combatState.IsOverOrEnding())
        {
            return Task.FromResult<IReadOnlyList<CardModel>>(Array.Empty<CardModel>());
        }

        return SelectCardsAsync(combatState, player, candidates, minCount, maxCount, source, cancelable);
    }
}
