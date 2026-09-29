using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class WoundTests : IDisposable
{
    public WoundTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_MatchesAuthoritativeSource()
    {
        Wound card = ModelDb.Card<Wound>();

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(CardType.Status, card.Type);
        Assert.Equal(CardRarity.Status, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Unplayable }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
    }

    [Fact]
    public async Task InHand_IsUnplayableAndDiscardsAtEndOfTurn()
    {
        (Player player, CombatRoom room) = await Task11CombatTestSupport.CreateCombatAsync("wound-discard");
        Wound wound = Task11CombatTestSupport.AddToHand<Wound>(player);

        Assert.False(wound.CanPlay(out UnplayableReason reason));
        Assert.Equal(UnplayableReason.HasUnplayableKeyword, reason);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Contains(wound, player.PlayerCombatState!.DiscardPile.Cards);
        Assert.DoesNotContain(wound, player.PlayerCombatState.ExhaustPile.Cards);
    }
}
