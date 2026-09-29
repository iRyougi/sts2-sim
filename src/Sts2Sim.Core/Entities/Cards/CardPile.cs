using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;

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
        List<CardModel> list = _cards.ToList();
        rng.Shuffle(list);
        _cards.Clear();
        _cards.AddRange(list);
    }
}
