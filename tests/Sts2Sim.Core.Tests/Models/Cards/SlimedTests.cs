using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class SlimedTests : IDisposable
{
    public SlimedTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_MatchesAuthoritativeSource()
    {
        Slimed card = ModelDb.Card<Slimed>();

        Assert.Equal(1, card.EnergyCost);
        Assert.Equal(CardType.Status, card.Type);
        Assert.Equal(CardRarity.Status, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Exhaust }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.DoesNotContain(CardKeyword.Unplayable, card.Keywords);
    }

    [Fact]
    public async Task Play_DrawsOneCardThenExhausts()
    {
        (Player player, CombatRoom room) = await Task11CombatTestSupport.CreateCombatAsync("slimed-draw");
        Slimed slimed = Task11CombatTestSupport.AddToHand<Slimed>(player);
        var drawnCard = (Debris)ModelDb.Card<Debris>().MutableClone();
        drawnCard.AssignOwner(player);
        await CardPileCmd.Generate(room.Engine.State, drawnCard, PileType.Draw, CardPilePosition.Top);
        int energyBefore = player.PlayerCombatState!.Energy;
        int handCountBefore = player.PlayerCombatState.Hand.Cards.Count;
        int drawCountBefore = player.PlayerCombatState.DrawPile.Cards.Count;

        await room.Engine.PlayCardAsync(player, slimed, target: null);

        Assert.Equal(energyBefore - 1, player.PlayerCombatState.Energy);
        Assert.Equal(handCountBefore, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(drawCountBefore - 1, player.PlayerCombatState.DrawPile.Cards.Count);
        Assert.Contains(drawnCard, player.PlayerCombatState!.Hand.Cards);
        Assert.Contains(slimed, player.PlayerCombatState.ExhaustPile.Cards);
    }
}
