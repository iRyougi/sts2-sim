using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GreedTests : IDisposable
{
    public GreedTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();
    [Fact]
    public void Metadata_MatchesSource()
    {
        var card = (Greed)ModelDb.Card<Greed>().MutableClone();

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(CardType.Curse, card.Type);
        Assert.Equal(CardRarity.Curse, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Eternal, CardKeyword.Unplayable }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.False(card.CanBeGeneratedInCombat);
    }

    [Fact]
    public async Task PersistentDeckCard_CannotBeRemovedOrTransformed()
    {
        Player player = CreatePlayer();
        Greed greed = AddToDeck<Greed>(player);
        var replacement = (Injury)ModelDb.Card<Injury>().MutableClone();

        Assert.False(greed.IsRemovable);
        Assert.False(greed.IsTransformable);

        Assert.Throws<InvalidOperationException>(() => CardPileCmd.Remove(greed));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CardCmd.Transform(greed, replacement));
        Assert.Contains(greed, player.Deck.Cards);
        Assert.DoesNotContain(replacement, player.Deck.Cards);
    }

    [Fact]
    public async Task MerchantRemoval_RejectionDoesNotChargeOrConsumeService()
    {
        Player player = CreatePlayer();
        player.Gold = 99999;
        Greed greed = AddToDeck<Greed>(player);
        var room = new MerchantRoom();
        await room.EnterInternal((RunState)player.RunState);
        int goldBefore = player.Gold;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            room.BuyCardRemoval(greed, player));

        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(0, player.CardRemovalsUsed);
        Assert.False(room.Inventory.CardRemoval.Purchased);
        Assert.Contains(greed, player.Deck.Cards);
    }

    [Fact]
    public void CombatCopy_RemainsTransientAndCanBeRemoved()
    {
        Player player = CreatePlayer();
        player.ResetCombatState();
        Greed greed = AddToDeck<Greed>(player);
        var combatCopy = (Greed)greed.MutableClone();
        combatCopy.AssignOwner(player);
        CardPileCmd.Add(combatCopy, PileType.Hand);

        Assert.True(combatCopy.IsRemovable);
        Assert.True(combatCopy.IsTransformable);

        CardPileCmd.Remove(combatCopy);

        Assert.Null(combatCopy.Pile);
        Assert.Contains(greed, player.Deck.Cards);
    }

    private static Player CreatePlayer()
    {
        var runState = new RunState("task11-greed", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }

    private static TCard AddToDeck<TCard>(Player player) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.Deck.AddInternal(card);
        return card;
    }
}
