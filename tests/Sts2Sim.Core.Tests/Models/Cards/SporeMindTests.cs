using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class SporeMindTests : IDisposable
{
    public SporeMindTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_MatchesSource()
    {
        var card = (SporeMind)ModelDb.Card<SporeMind>().MutableClone();

        Assert.Equal(1, card.EnergyCost);
        Assert.Equal(CardType.Curse, card.Type);
        Assert.Equal(CardRarity.Curse, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Exhaust }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.False(card.CanBeGeneratedInCombat);
        Assert.DoesNotContain(CardKeyword.Unplayable, card.Keywords);
    }

    [Fact]
    public async Task Play_IsAllowedAndMovesToExhaust()
    {
        var runState = new RunState("task11-spore-mind", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        var card = (SporeMind)ModelDb.Card<SporeMind>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);

        Assert.True(card.CanPlay(out UnplayableReason reason));
        Assert.Equal(UnplayableReason.None, reason);

        await card.PlayAsync(target: null);

        Assert.Contains(card, player.PlayerCombatState.ExhaustPile.Cards);
    }
}
