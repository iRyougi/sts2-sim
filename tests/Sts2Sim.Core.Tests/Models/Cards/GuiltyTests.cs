using Sts2Sim.Core.Commands;
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
public sealed class GuiltyTests : IDisposable
{
    public GuiltyTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_MatchesSource()
    {
        var card = (Guilty)ModelDb.Card<Guilty>().MutableClone();

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(CardType.Curse, card.Type);
        Assert.Equal(CardRarity.Curse, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Unplayable }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
    }

    [Fact]
    public async Task PersistentDeckCard_RemovesItselfAfterExactlyFiveCombatEnds()
    {
        var runState = new RunState("task11-guilty", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var guilty = (Guilty)ModelDb.Card<Guilty>().MutableClone();
        guilty.AssignOwner(player);
        player.Deck.AddInternal(guilty);

        for (int combat = 1; combat <= 4; combat++)
        {
            var room = new CombatRoom(() =>
                (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
            await room.Enter(runState);
            await CreatureCmd.Kill(room.Engine.State.Enemies.Single());
            Assert.True(room.Engine.CheckWinCondition());
            await room.ResolveOutcomeAsync(generateRewards: false);
            await room.Exit(runState);
            Assert.Contains(guilty, player.Deck.Cards);
        }

        var fifthRoom = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await fifthRoom.Enter(runState);
        await CreatureCmd.Kill(fifthRoom.Engine.State.Enemies.Single());
        Assert.True(fifthRoom.Engine.CheckWinCondition());
        await fifthRoom.ResolveOutcomeAsync(generateRewards: false);
        await fifthRoom.Exit(runState);

        Assert.DoesNotContain(guilty, player.Deck.Cards);
    }
}
