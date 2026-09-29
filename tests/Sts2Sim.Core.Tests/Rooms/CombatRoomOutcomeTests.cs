using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

sealed class CombatVictorySpyRelic : RelicModel
{
    public int VictoryCount { get; private set; }

    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task AfterCombatVictory()
    {
        VictoryCount++;
        return Task.CompletedTask;
    }
}

[Collection(nameof(ModelDbCollection))]
public class CombatRoomOutcomeTests
{
    private static async Task<(CombatRoom Room, Player Player, CombatVictorySpyRelic Spy)> CreateCombatAsync(string seed)
    {
        ModelDb.Inject(typeof(Regent));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Relics.DivineRight));
        ModelDb.Inject(typeof(StrikeRegent));
        ModelDb.Inject(typeof(DefendRegent));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Cards.FallingStar));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Cards.Venerate));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Powers.WeakPower));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Powers.VulnerablePower));
        ModelDb.Inject(typeof(WanderingGrunt));
        ModelDb.Inject(typeof(CombatVictorySpyRelic));

        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var spy = (CombatVictorySpyRelic)ModelDb.Relic<CombatVictorySpyRelic>().MutableClone();
        spy.AssignOwner(player);
        player.AddRelicInternal(spy);

        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.EnterInternal(runState);
        return (room, player, spy);
    }

    [Fact]
    public async Task ResolveOutcomeAsync_BeforeEngineVictory_DoesNotResolveOrFireHook()
    {
        (CombatRoom room, _, CombatVictorySpyRelic spy) = await CreateCombatAsync("combat-outcome-early");
        room.Engine.State.Enemies[0].LoseHpInternal(decimal.MaxValue, default);

        await room.ResolveOutcomeAsync();

        Assert.True(room.Engine.IsInProgress);
        Assert.False(room.Won);
        Assert.Equal(0, spy.VictoryCount);
    }

    [Fact]
    public async Task ResolveOutcomeAsync_AfterEngineVictory_FiresHookExactlyOnce()
    {
        (CombatRoom room, _, CombatVictorySpyRelic spy) = await CreateCombatAsync("combat-outcome-victory");
        room.Engine.State.Enemies[0].LoseHpInternal(decimal.MaxValue, default);
        room.Engine.CheckWinCondition();

        await Task.WhenAll(room.ResolveOutcomeAsync(), room.ResolveOutcomeAsync());
        Assert.NotNull(room.Rewards);
        object rewards = room.Rewards;
        await room.ResolveOutcomeAsync();

        Assert.True(room.Engine.Won);
        Assert.True(room.Won);
        Assert.Same(rewards, room.Rewards);
        Assert.Equal(1, spy.VictoryCount);
    }

    [Fact]
    public async Task ResolveOutcomeAsync_AfterDefeat_DoesNotResolveOrFireHook()
    {
        (CombatRoom room, Player player, CombatVictorySpyRelic spy) = await CreateCombatAsync("combat-outcome-defeat");
        player.Creature.GainBlockInternal(5m);
        player.Creature.LoseHpInternal(decimal.MaxValue, default);
        room.Engine.CheckWinCondition();

        await room.ResolveOutcomeAsync();
        await room.Exit(null);

        Assert.False(room.Engine.Won);
        Assert.False(room.Won);
        Assert.Equal(0, spy.VictoryCount);
        Assert.Equal(5, player.Creature.Block);
    }
}
