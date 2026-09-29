using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Hooks;

[Collection("ModelDb")]
public sealed class Plan08b4HookRegressionTests : IDisposable
{
    public Plan08b4HookRegressionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RoyalPoison_DamagesBeforeBloodVialHealsAtFirstPlayerTurn()
    {
        var (run, player) = CreateRun();
        player.Creature.SetMaxHpInternal(50m);
        player.Creature.SetCurrentHpInternal(50m);
        await RelicCmd.Obtain(ModelDb.Relic<RoyalPoison>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<BloodVial>(), player);
        var room = CreateRoom();

        await room.Enter(run);

        // Upstream normal phase deals 4, then late phase heals 2: 50 -> 46 -> 48.
        Assert.Equal(48, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task TargetedPotion_RejectsLivingUnhittableEnemyWithoutConsumingSlot()
    {
        var (run, player) = CreateRun();
        var room = CreateRoom();
        await room.Enter(run);
        var enemy = room.Engine.State.Enemies.Single();
        await PowerCmd.Apply<IllusionPower>(room.Engine.State, enemy, 1m, null, null);
        await enemy.GetPower<IllusionPower>()!.AfterDeath(enemy);
        Assert.True(enemy.IsAlive);
        Assert.False(enemy.IsHittable);
        var potion = player.AddPotionInternal(ModelDb.Potion<PoisonPotion>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, player, enemy));

        Assert.Contains(potion, player.PotionSlots);
        Assert.False(enemy.HasPower<PoisonPower>());
    }

    [Fact]
    public async Task PreciseScissors_RemovesThePlayersChosenCardInsteadOfFirstCandidate()
    {
        var (run, player) = CreateRun();
        CardModel first = player.Deck.Cards.First(card => card.IsRemovable);
        CardModel chosen = player.Deck.Cards.Last(card => card.IsRemovable);
        run.ConfigureCardSelectionSource(new ChosenCardSource(chosen));

        await RelicCmd.Obtain(ModelDb.Relic<PreciseScissors>(), player);

        Assert.DoesNotContain(chosen, player.Deck.Cards);
        Assert.Contains(first, player.Deck.Cards);
    }

    private static (RunState Run, Player Player) CreateRun()
    {
        var run = new RunState("plan08b4-hooks", new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }

    private static CombatRoom CreateRoom() => new(() =>
        (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());

    private sealed class ChosenCardSource(CardModel chosen) : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
            Task.FromResult<IReadOnlyList<CardModel>>([chosen]);
    }
}
