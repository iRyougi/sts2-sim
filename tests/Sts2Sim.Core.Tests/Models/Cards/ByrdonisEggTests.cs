using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class ByrdonisEggTests : IDisposable
{
    public ByrdonisEggTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task MetadataAndPlayability_MatchSource()
    {
        (Player player, _) = await Task11CombatTestSupport.CreateCombatAsync("task12-egg-metadata");
        ByrdonisEgg egg = Task11CombatTestSupport.AddToHand<ByrdonisEgg>(player);

        Assert.Equal(-1, egg.EnergyCost);
        Assert.Equal(CardType.Quest, egg.Type);
        Assert.Equal(CardRarity.Quest, egg.Rarity);
        Assert.Equal(TargetType.None, egg.TargetType);
        Assert.Equal(new[] { CardKeyword.Unplayable }, egg.Keywords);
        Assert.Equal(0, egg.MaxUpgradeLevel);
        Assert.False(egg.CanPlay(out UnplayableReason reason));
        Assert.True(reason.HasFlag(UnplayableReason.HasUnplayableKeyword));
    }

    [Fact]
    public void HasEventPet_AndHatchDecision_AreScopedToEggOwner()
    {
        var runState = new RunState("task12-egg-owner", new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player unrelatedPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(unrelatedPlayer);
        var egg = (ByrdonisEgg)ModelDb.Card<ByrdonisEgg>().MutableClone();
        egg.AssignOwner(owner);
        owner.Deck.AddInternal(egg);
        var room = new RestSiteRoom();

        Assert.True(owner.HasEventPet());
        Assert.False(unrelatedPlayer.HasEventPet());
        Assert.Single(room.GetAvailableDecisions(runState, owner).OfType<RestSiteDecision.Hatch>());
        Assert.Empty(room.GetAvailableDecisions(runState, unrelatedPlayer).OfType<RestSiteDecision.Hatch>());
    }
}
