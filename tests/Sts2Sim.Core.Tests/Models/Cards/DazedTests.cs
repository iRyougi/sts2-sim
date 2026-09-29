using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class DazedTests : IDisposable
{
    public DazedTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_MatchesAuthoritativeSource()
    {
        Dazed card = ModelDb.Card<Dazed>();

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(CardType.Status, card.Type);
        Assert.Equal(CardRarity.Status, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Ethereal, CardKeyword.Unplayable }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
    }

    [Fact]
    public async Task InHand_IsUnplayableAndExhaustsAtEndOfTurn()
    {
        (Player player, CombatRoom room) = await Task11CombatTestSupport.CreateCombatAsync("dazed-ethereal");
        Dazed dazed = Task11CombatTestSupport.AddToHand<Dazed>(player);

        Assert.False(dazed.CanPlay(out UnplayableReason reason));
        Assert.Equal(UnplayableReason.HasUnplayableKeyword, reason);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Contains(dazed, player.PlayerCombatState!.ExhaustPile.Cards);
    }
}
