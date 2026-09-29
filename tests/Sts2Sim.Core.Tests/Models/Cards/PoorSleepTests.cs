using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class PoorSleepTests : IDisposable
{
    public PoorSleepTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_MatchesSource()
    {
        var card = (PoorSleep)ModelDb.Card<PoorSleep>().MutableClone();

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(CardType.Curse, card.Type);
        Assert.Equal(CardRarity.Curse, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Unplayable, CardKeyword.Retain }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
    }

    [Fact]
    public void ModifierGenerationRestriction_MapsToCombatGenerationFlag()
    {
        PoorSleep card = ModelDb.Card<PoorSleep>();

        Assert.False(card.CanBeGeneratedInCombat);
    }

    [Fact]
    public async Task EndPlayerTurn_RetainsPoorSleepInHand()
    {
        (var player, var room) = await Task11CombatTestSupport.CreateCombatAsync("task11-poor-sleep");
        PoorSleep card = Task11CombatTestSupport.AddToHand<PoorSleep>(player);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Contains(card, player.PlayerCombatState!.Hand.Cards);
    }
}
