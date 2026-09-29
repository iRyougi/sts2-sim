using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class NecrobinderCharacterTests : IDisposable
{
    public NecrobinderCharacterTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task StartingRun_MatchesSource()
    {
        // Necrobinder.StartingDeck and BoundPhylactery, v0.111.0.
        Type[] sourceDeck =
        [
            typeof(StrikeNecrobinder), typeof(StrikeNecrobinder), typeof(StrikeNecrobinder),
            typeof(StrikeNecrobinder), typeof(DefendNecrobinder), typeof(DefendNecrobinder),
            typeof(DefendNecrobinder), typeof(DefendNecrobinder), typeof(Bodyguard), typeof(Unleash),
        ];

        var run = new RunState("necrobinder-start-source", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Necrobinder>(), run);
        run.AddPlayer(player);

        Assert.Equal(66, player.Creature.CurrentHp);
        Assert.Equal(66, player.Creature.MaxHp);
        Assert.Equal(99, player.Gold);
        Assert.Equal(3, player.MaxEnergy);
        Assert.Equal(sourceDeck, player.Deck.Cards.Select(card => card.GetType()));
        Assert.All(player.Deck.Cards, card => Assert.Same(player, card.Owner));
        BoundPhylactery phylactery = Assert.IsType<BoundPhylactery>(Assert.Single(player.Relics));
        Assert.Same(player, phylactery.Owner);
        Assert.Null(player.Osty);

        // BoundPhylactery summons 1 before combat starts; turn 1's energy reset does not summon again.
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        Assert.True(player.IsOstyAlive);
        Assert.Equal(1, player.Osty!.MaxHp);
        Assert.Equal(1, player.Osty.CurrentHp);
        Assert.Same(player, player.Osty.PetOwner);
        Assert.NotNull(player.Osty.GetPower<DieForYouPower>());
    }
}
