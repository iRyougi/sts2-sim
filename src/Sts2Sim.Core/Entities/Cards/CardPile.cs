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

    /// <summary>
    /// sls2（观测格式 20，储君）：抽牌堆从顶部起有几张是玩家知道位置的。同 Sts2Headless 的 Observation.DrawTop（游戏上挂的补丁）：
    /// 经 CardPileCmd 按 Top 放进来的牌加一张，按 Random 放进来的整段作废，按 Bottom 不变；不是经 CardPileCmd 按位置放、而是按下标插入的
    /// （变化的替换牌插回原位）只保留插入点以上；移走顶部已知段里的牌（抽走、打出、选走、挪到底部）减一张；挪到顶部（固有牌）加一张；
    /// 打乱（开局洗牌、Session.Reseed 只打乱已知段以下的部分除外）作废。只记张数：牌的先后与游戏一致时，前缀张数就是游戏里核对一致的那段。
    /// 只对抽牌堆记；CardPileCmd.Shuffle 的 StableShuffle 经 RandomizeOrderInternal 以外的路径重排时由那里清零。克隆时照抄。
    /// </summary>
    public int KnownTop { get; set; }

    /// <summary>CardPileCmd.GetInsertionIndex 记下的下一次 AddInternal 的位置（Top / Random / Bottom），用完就清。</summary>
    internal CardPilePosition? PendingPosition;

    public void AddInternal(CardModel card, int index = -1)
    {
        CardPilePosition? pos = PendingPosition;
        PendingPosition = null;
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
        if (Type != PileType.Draw) return;
        if (pos == CardPilePosition.Top) KnownTop = Math.Min(KnownTop + 1, _cards.Count);
        else if (pos == CardPilePosition.Random) KnownTop = 0;
        else if (pos == null && index >= 0 && index < _cards.Count - 1) KnownTop = Math.Min(KnownTop, index);
    }

    public void RemoveInternal(CardModel card)
    {
        int i = Type == PileType.Draw ? _cards.IndexOf(card) : -1;
        if (_cards.Remove(card) && card.Pile == this)
        {
            card.AssignPileInternal(null);
        }
        if (i >= 0 && i < KnownTop) KnownTop--;
    }

    public void MoveToBottomInternal(CardModel card)
    {
        int i = Type == PileType.Draw ? _cards.IndexOf(card) : -1;
        _cards.Remove(card);
        _cards.Add(card);
        if (i >= 0 && i < KnownTop) KnownTop--;
    }

    public void MoveToTopInternal(CardModel card)
    {
        int i = Type == PileType.Draw ? _cards.IndexOf(card) : -1;
        _cards.Remove(card);
        _cards.Insert(0, card);
        if (Type == PileType.Draw && (i < 0 || i >= KnownTop)) KnownTop = Math.Min(KnownTop + 1, _cards.Count);
    }

    /// <summary>开局使用不稳定洗牌；普通重洗由 CardPileCmd.Shuffle 的 StableShuffle 处理。</summary>
    public void RandomizeOrderInternal(Rng rng)
    {
        List<CardModel> list = _cards.ToList();
        rng.Shuffle(list);
        _cards.Clear();
        _cards.AddRange(list);
        KnownTop = 0;
    }

    /// <summary>sls2：搜索的确定化（Session.Reseed）只打乱玩家不知道位置的部分，已知的顶部原样留着。</summary>
    public void RandomizeBelowKnownTop(Rng rng)
    {
        List<CardModel> rest = _cards.Skip(KnownTop).ToList();
        rng.Shuffle(rest);
        _cards.RemoveRange(KnownTop, _cards.Count - KnownTop);
        _cards.AddRange(rest);
    }
}
