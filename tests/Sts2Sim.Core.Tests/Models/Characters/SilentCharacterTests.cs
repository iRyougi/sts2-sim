namespace Sts2Sim.Core.Tests.Models.Characters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class SilentCharacterTests : IDisposable
{
    public SilentCharacterTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Silent_HasRealStartingStats()
    {
        Silent silent = ModelDb.Character<Silent>();

        Assert.Equal(70, silent.StartingHp);
        Assert.Equal(99, silent.StartingGold);
        Assert.Equal(3, silent.MaxEnergy);
    }

    [Fact]
    public void Silent_StartingDeck_IsTwelveCards()
    {
        Silent silent = ModelDb.Character<Silent>();

        Assert.Equal(12, silent.StartingDeck.Count);
        Assert.Equal(5, silent.StartingDeck.Count(t => t == typeof(StrikeSilent)));
        Assert.Equal(5, silent.StartingDeck.Count(t => t == typeof(DefendSilent)));
        Assert.Single(silent.StartingDeck, t => t == typeof(Neutralize));
        Assert.Single(silent.StartingDeck, t => t == typeof(Survivor));
    }

    [Fact]
    public void Silent_StartsWithRingOfTheSnake()
    {
        Silent silent = ModelDb.Character<Silent>();

        Assert.Equal(new[] { typeof(RingOfTheSnake) }, silent.StartingRelics);
    }

    [Fact]
    public void Silent_CanCreateRunState()
    {
        var runState = new RunState("silent-smoke", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);

        Assert.Equal(70, player.Creature.CurrentHp);
        Assert.Equal(12, player.Deck.Cards.Count);
        Assert.Equal(3, player.MaxEnergy);
    }

    [Fact]
    public void SilentCardPool_UsesAuthoritativeOrderAndIncludesStartersAndImplementedBatches()
    {
        IReadOnlyList<CardModel> cards = ModelDb.Character<Silent>().CardPool.AllCards;

        Assert.Equal(91, cards.Count);
        Assert.Equal(91, cards.Select(card => card.GetType()).Distinct().Count());
        Assert.Equal(
            new[] { typeof(Abrasive), typeof(Accelerant), typeof(Accuracy), typeof(Acrobatics) },
            cards.Take(4).Select(card => card.GetType()));
        Assert.Contains(cards, card => card is StrikeSilent);
        Assert.Contains(cards, card => card is DefendSilent);
        Assert.Contains(cards, card => card is Neutralize);
        Assert.Contains(cards, card => card is Survivor);
        Assert.Contains(cards, card => card is Adrenaline);
        Assert.Contains(cards, card => card is Accelerant);
        Assert.Contains(cards, card => card is Accuracy);
    }

    [Fact]
    public void SilentCardPool_HasNinetyOneCards()
    {
        Assert.Equal(91, ModelDb.Character<Silent>().CardPool.AllCards.Count);
    }

    [Fact]
    public void RingOfTheSnake_AddsTwoDrawOnlyForOwnerOnFirstTurn()
    {
        var runState = new RunState("silent-ring", new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        Player foreign = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(foreign);
        var combatState = new CombatState(runState);
        foreach (Player player in runState.Players)
        {
            player.ResetCombatState();
            combatState.AddPlayerCreature(player.Creature);
            player.PlayerCombatState!.TurnNumber = 1;
        }

        RingOfTheSnake ring = Assert.Single(owner.Relics.OfType<RingOfTheSnake>());

        Assert.Equal(7m, Hook.ModifyHandDraw(combatState, owner, 5m));
        Assert.Equal(5m, ring.ModifyHandDraw(foreign, 5m));

        owner.PlayerCombatState!.TurnNumber = 2;
        Assert.Equal(5m, ring.ModifyHandDraw(owner, 5m));
    }
}
