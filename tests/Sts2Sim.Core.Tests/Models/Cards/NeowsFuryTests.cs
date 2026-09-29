using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class NeowsFuryTests : IDisposable
{
    public NeowsFuryTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 10, 2)]
    [InlineData(true, 14, 3)]
    public async Task Play_DealsDamageAndRetrievesBoundedDiscardCards(
        bool upgraded,
        int expectedDamage,
        int expectedRetrieved)
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            $"task11-neows-fury-{upgraded}");
        NeowsFury fury = Task11CombatTestSupport.AddToHand<NeowsFury>(player);
        if (upgraded)
        {
            fury.Upgrade();
        }
        StrikeRegent[] discard = Enumerable.Range(0, 4)
            .Select(_ => Task11CombatTestSupport.AddToHand<StrikeRegent>(player))
            .ToArray();
        foreach (StrikeRegent card in discard)
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
        decimal hpBefore = room.Engine.State.Enemies[0].CurrentHp;

        await fury.PlayAsync(room.Engine.State.Enemies[0]);

        Assert.Equal(expectedDamage, hpBefore - room.Engine.State.Enemies[0].CurrentHp);
        Assert.Equal(discard.Take(expectedRetrieved),
            player.PlayerCombatState!.Hand.Cards.Where(discard.Contains));
        Assert.All(discard.Skip(expectedRetrieved), card =>
            Assert.Contains(card, player.PlayerCombatState.DiscardPile.Cards));
    }

    [Fact]
    public async Task Play_WhenHandHasNoCapacity_LeavesDiscardCardsInDiscard()
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            "task11-neows-fury-full");
        NeowsFury fury = Task11CombatTestSupport.AddToHand<NeowsFury>(player);
        StrikeRegent[] discard = Enumerable.Range(0, 2)
            .Select(_ => Task11CombatTestSupport.AddToHand<StrikeRegent>(player))
            .ToArray();
        foreach (StrikeRegent card in discard)
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
        while (player.PlayerCombatState!.Hand.Cards.Count <= CardPile.MaxCardsInHand)
        {
            Task11CombatTestSupport.AddToHand<StrikeRegent>(player);
        }

        await fury.PlayAsync(room.Engine.State.Enemies[0]);

        Assert.All(discard, card => Assert.Contains(card, player.PlayerCombatState.DiscardPile.Cards));
    }

    [Fact]
    public void Metadata_MatchesSource()
    {
        CardModel card = ModelDb.Card<NeowsFury>();
        Assert.Equal(1, card.EnergyCost);
        Assert.Equal(CardType.Attack, card.Type);
        Assert.Equal(CardRarity.Ancient, card.Rarity);
        Assert.Equal(TargetType.AnyEnemy, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Exhaust }, card.Keywords);
        Assert.False(card.CanBeGeneratedInCombat);
    }
}
