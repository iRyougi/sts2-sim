using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Entities.Cards;

/// <summary>
/// 一种牌堆容器。逐字移植增删/重排核心（<c>MegaCrit.Sts2.Core.Entities.Cards.CardPile</c>）；
/// <c>Get(PileType, Player)</c> 静态查找放在 <c>CardPileCmd</c>（Task 4）而非这里,避免 CardPile
/// 反向依赖 Player。
/// </summary>
public sealed class CardPile(PileType type)
{
    private readonly List<CardModel> _cards = new();

    public const int MaxCardsInHand = 10;

    public PileType Type { get; } = type;

    public IReadOnlyList<CardModel> Cards => _cards;

    /// <summary>The same list as <see cref="Cards"/>, for allocation- and interface-free reads in hook dispatch (Plan 09-O).</summary>
    internal List<CardModel> CardList => _cards;

    public void AddInternal(CardModel card, int index = -1)
    {
        if (card.Pile != null)
        {
            card.Pile.RemoveInternal(card);
        }

        if (index < 0 || index > _cards.Count)
        {
            _cards.Add(card);
        }
        else
        {
            _cards.Insert(index, card);
        }
        card.AssignPileInternal(this);

        if (Type is not (PileType.None or PileType.Deck) &&
            card.Owner is { PlayerCombatState: { } pcs } owner &&
            owner.Creature.CombatState is { } combatState &&
            combatState.IsLiveCombat() &&
            card.Tags.Contains(CardTag.Shoot) &&
            owner.Character.BaseArrowSlotCount == 0 &&
            pcs.Arrows.Count == 0)
        {
            ArrowCmd.GainArrowSlot(owner);
        }
    }

    public void RemoveInternal(CardModel card)
    {
        if (_cards.Remove(card) && card.Pile == this)
        {
            card.AssignPileInternal(null);
        }
    }

    public void MoveToBottomInternal(CardModel card)
    {
        _cards.Remove(card);
        _cards.Add(card);
    }

    public void MoveToTopInternal(CardModel card)
    {
        _cards.Remove(card);
        _cards.Insert(0, card);
    }

    /// <summary>开局使用不稳定洗牌；普通重洗由 CardPileCmd.Shuffle 的 StableShuffle 处理。</summary>
    public void RandomizeOrderInternal(Rng rng)
    {
        // 原版 v0.113.1：_cards.UnstableShuffle(rng)。
        List<CardModel> list = _cards.ToList();
        list.UnstableShuffle(rng);
        _cards.Clear();
        _cards.AddRange(list);
    }
}
