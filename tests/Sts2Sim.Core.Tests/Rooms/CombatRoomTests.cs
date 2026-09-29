using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

[CollectionDefinition(nameof(ModelDbCollection), DisableParallelization = true)]
public sealed class ModelDbCollection;

[Collection(nameof(ModelDbCollection))]
public class CombatRoomTests
{
    private static void EnsureModels()
    {
        ModelDb.Inject(typeof(Regent));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Relics.DivineRight));
        ModelDb.Inject(typeof(StrikeRegent));
        ModelDb.Inject(typeof(DefendRegent));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Cards.FallingStar));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Cards.Venerate));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Powers.WeakPower));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Powers.VulnerablePower));
        ModelDb.Inject(typeof(Sts2Sim.Core.Models.Powers.StrengthPower));
        ModelDb.Inject(typeof(WanderingGrunt));
    }

    [Fact]
    public async Task EnterInternal_StartsCombatWithGivenMonster()
    {
        EnsureModels();
        var runState = new RunState("combat-room-a", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.EnterInternal(runState);

        Assert.True(room.Engine.IsInProgress);
        Assert.Single(room.Engine.State.Enemies);
    }

    [Fact]
    public async Task ResolveOutcomeAsync_AfterVictory_SetsWonAndFiresAfterCombatVictory()
    {
        EnsureModels();
        var runState = new RunState("combat-room-victory", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.EnterInternal(runState);
        while (room.Engine.IsInProgress)
        {
            CardModel? playable = player.PlayerCombatState!.Hand.Cards.FirstOrDefault(c => c.CanPlay(out _));
            if (playable is not null)
            {
                Sts2Sim.Core.Entities.Creatures.Creature? target = playable.TargetType == Sts2Sim.Core.Entities.Cards.TargetType.AnyEnemy
                    ? room.Engine.State.HittableEnemies.FirstOrDefault()
                    : null;
                await room.Engine.PlayCardAsync(player, playable, target);
                room.Engine.CheckWinCondition();
            }
            else if (room.Engine.IsInProgress)
            {
                await room.Engine.EndPlayerTurnAsync();
            }
        }

        await room.ResolveOutcomeAsync();

        Assert.True(room.Won);
        Assert.NotNull(room.Rewards);
    }

    [Fact]
    public void RoomStack_PushAndPopRoundTrips()
    {
        EnsureModels();
        var runState = new RunState("combat-room-b", new Overgrowth());
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());

        runState.PushRoom(room);
        Assert.Same(room, runState.CurrentRoom);

        AbstractRoom popped = runState.PopCurrentRoom();
        Assert.Same(room, popped);
        Assert.Null(runState.CurrentRoom);
    }

    [Fact]
    public async Task Exit_ClearsCombatPowersBlockAndStateBeforeTheNextCombat()
    {
        EnsureModels();
        var runState = new RunState("combat-room-cleanup", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var firstRoom = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await firstRoom.EnterInternal(runState);
        await PowerCmd.Apply<Sts2Sim.Core.Models.Powers.StrengthPower>(
            firstRoom.Engine.State, player.Creature, 3m, player.Creature, null);
        player.Creature.GainBlockInternal(7m);

        await firstRoom.Exit(runState);

        Assert.Empty(player.Creature.Powers);
        Assert.Equal(0, player.Creature.Block);
        Assert.Null(player.Creature.CombatState);
        Assert.Null(player.PlayerCombatState);

        var secondRoom = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await secondRoom.EnterInternal(runState);
        Assert.Empty(player.Creature.Powers);
        Assert.Same(secondRoom.Engine.State, player.Creature.CombatState);
        Assert.NotNull(player.PlayerCombatState);
    }

    [Fact]
    public void LegacyConstructorSignatures_RemainAvailableForCompiledConsumers()
    {
        Type roomType = typeof(CombatRoom);
        Type slottedBatchType = typeof(IReadOnlyList<(MonsterModel Monster, string? SlotName)>);

        Assert.NotNull(roomType.GetConstructor(
            [typeof(Func<MonsterModel>), typeof(RoomType)]));
        Assert.NotNull(roomType.GetConstructor(
            [typeof(Func<>).MakeGenericType(typeof(IReadOnlyList<MonsterModel>)), typeof(RoomType)]));
        Assert.NotNull(roomType.GetConstructor(
            [
                typeof(Func<>).MakeGenericType(slottedBatchType),
                typeof(RoomType),
            ]));
    }
}
