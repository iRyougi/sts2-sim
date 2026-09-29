namespace Sts2Sim.Core.Tests.Entities.Cards;

using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Random;

[Collection("ModelDb")]
public class CardPileTests
{
    public CardPileTests()
    {
        Sts2Sim.Core.Models.ModelDb.ResetForTests();
        Sts2Sim.Core.Models.ModelDb.Init(new[] { typeof(StrikeRegent) });
    }

    private static StrikeRegent NewCard() => (StrikeRegent)Sts2Sim.Core.Models.ModelDb.Card<StrikeRegent>().MutableClone();

    [Fact]
    public void AddInternal_DefaultsToBottom()
    {
        var pile = new CardPile(PileType.Hand);
        var a = NewCard();
        var b = NewCard();

        pile.AddInternal(a);
        pile.AddInternal(b);

        Assert.Equal(new[] { a, b }, pile.Cards);
    }

    [Fact]
    public void AddInternal_ExplicitIndex_Inserts()
    {
        var pile = new CardPile(PileType.Hand);
        var a = NewCard();
        var b = NewCard();
        pile.AddInternal(a);

        pile.AddInternal(b, 0);

        Assert.Equal(new[] { b, a }, pile.Cards);
    }

    [Fact]
    public void RemoveInternal_RemovesCard()
    {
        var pile = new CardPile(PileType.Discard);
        var a = NewCard();
        pile.AddInternal(a);

        pile.RemoveInternal(a);

        Assert.Empty(pile.Cards);
    }

    [Fact]
    public void AddInternal_SetsCardPileMembership()
    {
        var pile = new CardPile(PileType.Hand);
        var card = NewCard();

        pile.AddInternal(card);

        Assert.Same(pile, card.Pile);
    }

    [Fact]
    public void AddInternal_MovesCardOutOfPreviousPile()
    {
        var drawPile = new CardPile(PileType.Draw);
        var discardPile = new CardPile(PileType.Discard);
        var card = NewCard();
        drawPile.AddInternal(card);

        discardPile.AddInternal(card);

        Assert.DoesNotContain(card, drawPile.Cards);
        Assert.Contains(card, discardPile.Cards);
        Assert.Same(discardPile, card.Pile);
    }

    [Fact]
    public void RemoveInternal_ClearsCardPileMembership()
    {
        var pile = new CardPile(PileType.Discard);
        var card = NewCard();
        pile.AddInternal(card);

        pile.RemoveInternal(card);

        Assert.Null(card.Pile);
    }

    [Fact]
    public void MoveToTopAndBottomInternal_Reorder()
    {
        var pile = new CardPile(PileType.Draw);
        var a = NewCard();
        var b = NewCard();
        var c = NewCard();
        pile.AddInternal(a);
        pile.AddInternal(b);
        pile.AddInternal(c);

        pile.MoveToTopInternal(c);
        Assert.Equal(new[] { c, a, b }, pile.Cards);

        pile.MoveToBottomInternal(c);
        Assert.Equal(new[] { a, b, c }, pile.Cards);
    }

    [Fact]
    public void RandomizeOrderInternal_IsDeterministic_ForSameSeed()
    {
        var pile1 = new CardPile(PileType.Draw);
        var pile2 = new CardPile(PileType.Draw);
        List<Sts2Sim.Core.Models.CardModel> cards1 = Enumerable.Range(0, 8).Select(_ => NewCard()).ToList<Sts2Sim.Core.Models.CardModel>();
        List<Sts2Sim.Core.Models.CardModel> cards2 = Enumerable.Range(0, 8).Select(_ => NewCard()).ToList<Sts2Sim.Core.Models.CardModel>();
        foreach (var c in cards1) pile1.AddInternal(c);
        foreach (var c in cards2) pile2.AddInternal(c);
        var originalOrder = pile1.Cards.ToList();

        pile1.RandomizeOrderInternal(new Rng(42uL));
        pile2.RandomizeOrderInternal(new Rng(42uL));

        Assert.Equal(
            pile1.Cards.Select(c => cards1.IndexOf(c)),
            pile2.Cards.Select(c => cards2.IndexOf(c)));
        // 同一 seed 产生相同结果只证明了确定性,不能证明真的洗过牌——8 张牌下,真实洗牌复现
        // 原始插入顺序的概率是天文数字级别的低,这里直接断言洗牌后顺序变化,拒绝空洞通过。
        Assert.NotEqual(
            Enumerable.Range(0, originalOrder.Count),
            pile1.Cards.Select(c => originalOrder.IndexOf(c)));
    }
}
