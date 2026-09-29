using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

file sealed class ForcedCombatBatchContractEvent : EventModel
{
    public void RequestSingle(Func<MonsterModel> factory) => RequestForcedCombat(factory);

    public void RequestBatch(Func<IReadOnlyList<MonsterModel>> factory) =>
        RequestForcedCombatBatch(factory);

    protected override IReadOnlyList<Sts2Sim.Core.Events.EventOption> GenerateInitialOptions() =>
        Array.Empty<Sts2Sim.Core.Events.EventOption>();
}

[Collection("ModelDb")]
public sealed class EventForcedCombatBatchTests : IDisposable
{
    public EventForcedCombatBatchTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(ForcedCombatBatchContractEvent),
            typeof(WanderingGrunt),
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void PendingForcedCombatBatch_PreservesFactoryIdentityAndDequeuesOnce()
    {
        var ev = (ForcedCombatBatchContractEvent)ModelDb
            .Event<ForcedCombatBatchContractEvent>()
            .MutableClone();
        Func<IReadOnlyList<MonsterModel>> factory = CreateFourMonsters;

        ev.RequestBatch(factory);

        Assert.True(ev.HasPendingForcedCombat);
        Assert.Same(factory, ev.DequeuePendingForcedCombatBatch());
        Assert.False(ev.HasPendingForcedCombat);
        Assert.Throws<InvalidOperationException>(() => ev.DequeuePendingForcedCombatBatch());
    }

    [Fact]
    public void PendingForcedCombat_RejectsCrossKindDuplicatesWithoutLosingTheFirstFactory()
    {
        var batchFirst = (ForcedCombatBatchContractEvent)ModelDb
            .Event<ForcedCombatBatchContractEvent>()
            .MutableClone();
        Func<IReadOnlyList<MonsterModel>> batch = CreateFourMonsters;
        Func<MonsterModel> single = () =>
            (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone();

        batchFirst.RequestBatch(batch);
        Assert.Throws<InvalidOperationException>(() => batchFirst.RequestSingle(single));
        Assert.Same(batch, batchFirst.DequeuePendingForcedCombatBatch());

        var singleFirst = (ForcedCombatBatchContractEvent)ModelDb
            .Event<ForcedCombatBatchContractEvent>()
            .MutableClone();
        singleFirst.RequestSingle(single);
        Assert.Throws<InvalidOperationException>(() => singleFirst.RequestBatch(batch));
        Assert.Same(single, singleFirst.DequeuePendingForcedCombat());
    }

    [Fact]
    public async Task CombatRoom_BatchRejectsNullBeforeMutatingEarlierMonsters()
    {
        var runState = CreateRun("forced-combat-batch-null");
        MonsterModel valid = (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone();
        var room = new CombatRoom(() => new MonsterModel[] { valid, null! });

        await Assert.ThrowsAsync<InvalidOperationException>(() => room.Enter(runState));

        Assert.Null(valid.Creature);
    }

    [Fact]
    public async Task CombatRoom_BatchRejectsDuplicateReferenceBeforeMutation()
    {
        var runState = CreateRun("forced-combat-batch-duplicate");
        MonsterModel duplicate = (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone();
        var room = new CombatRoom(() => new[] { duplicate, duplicate });

        await Assert.ThrowsAsync<InvalidOperationException>(() => room.Enter(runState));

        Assert.Null(duplicate.Creature);
    }

    [Fact]
    public async Task CombatRoom_BatchFactoryAddsAllFourMonstersToOneCombatState()
    {
        var runState = new RunState("forced-combat-batch-room", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(CreateFourMonsters);
        runState.PushRoom(room);

        await room.Enter(runState);

        Assert.Equal(4, room.Engine.State.Enemies.Count);
        Assert.All(room.Engine.State.Enemies, enemy => Assert.IsType<WanderingGrunt>(enemy.Monster));
        Assert.Equal((uint?)0, player.Creature.CombatId);
        Assert.Equal(new uint?[] { 1, 2, 3, 4 }, room.Engine.State.Enemies.Select(enemy => enemy.CombatId));
    }

    private static RunState CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Regent>(), runState));
        return runState;
    }

    private static IReadOnlyList<MonsterModel> CreateFourMonsters() =>
        Enumerable.Range(0, 4)
            .Select(_ => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone())
            .ToList()
            .AsReadOnly();
}
