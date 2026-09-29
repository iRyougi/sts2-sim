using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Enchantments;

[Collection("ModelDb")]
public sealed class SlitherTests : IDisposable
{
    public SlitherTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[] { typeof(SlitherXCard) }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CanEnchant_RejectsUnplayableAndXCostCards()
    {
        var slither = (Slither)ModelDb.Get(typeof(Slither)).MutableClone();
        var strike = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        var poorSleep = (PoorSleep)ModelDb.Card<PoorSleep>().MutableClone();
        var xCard = (SlitherXCard)ModelDb.Card<SlitherXCard>().MutableClone();

        Assert.True(slither.CanEnchant(strike));
        Assert.False(slither.CanEnchant(poorSleep));
        Assert.False(slither.CanEnchant(xCard));
    }

    [Fact]
    public async Task Draw_UsesDedicatedRng_PersistsThroughTurn_RerollsOnRedraw_AndClearsOnClone()
    {
        const string seed = "task11-slither";
        var expectedRng = new RunRngSet(seed);
        int firstExpected = expectedRng.CombatEnergyCosts.NextInt(4);
        int secondExpected = expectedRng.CombatEnergyCosts.NextInt(4);
        (var player, var room) = await Task11CombatTestSupport.CreateCombatAsync(seed);
        StrikeRegent card = Task11CombatTestSupport.AddToHand<StrikeRegent>(player);
        await CardCmd.Enchant<Slither>(card, 1m);
        CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);

        await CardPileCmd.Draw(room.Engine.State, 1, player, fromHandDraw: false);

        Assert.Equal(firstExpected, card.EnergyCost);
        Assert.Equal(1, room.Engine.State.RunState.Rng.CombatEnergyCosts.Counter);

        await room.Engine.EndPlayerTurnAsync();
        Assert.Equal(firstExpected, card.EnergyCost);
        CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(room.Engine.State, 1, player, fromHandDraw: false);

        Assert.Equal(secondExpected, card.EnergyCost);
        var freshClone = (StrikeRegent)card.MutableClone();
        Assert.Equal(1, freshClone.EnergyCost);
    }

    [Fact]
    public async Task CostPrecedence_IsTurnOverrideThenFreeThenCombatThenCanonical()
    {
        (var player, _) = await Task11CombatTestSupport.CreateCombatAsync("task11-slither-precedence");
        StrikeRegent card = Task11CombatTestSupport.AddToHand<StrikeRegent>(player);

        card.SetTemporaryCostOverrideThisCombat(2);
        Assert.Equal(2, card.EnergyCost);
        card.MakeTemporaryFreeThisTurn();
        Assert.Equal(0, card.EnergyCost);
        card.SetTemporaryCostOverrideThisTurn(3);
        Assert.Equal(3, card.EnergyCost);
        Assert.Equal(1, ((StrikeRegent)card.MutableClone()).EnergyCost);
    }

    [Fact]
    public async Task Draw_WhenHandIsFull_DoesNotMoveCardOrConsumeRng()
    {
        (var player, var room) = await Task11CombatTestSupport.CreateCombatAsync("task11-slither-full");
        StrikeRegent card = Task11CombatTestSupport.AddToHand<StrikeRegent>(player);
        await CardCmd.Enchant<Slither>(card, 1m);
        CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
        while (player.PlayerCombatState!.Hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            Task11CombatTestSupport.AddToHand<StrikeRegent>(player);
        }

        await CardPileCmd.Draw(room.Engine.State, 1, player, fromHandDraw: false);

        Assert.Contains(card, player.PlayerCombatState.DrawPile.Cards);
        Assert.Equal(0, room.Engine.State.RunState.Rng.CombatEnergyCosts.Counter);
    }
}
