using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public sealed class AncientPoolTests : IDisposable
{
    private static readonly IReadOnlyList<Type> MultiAncientPool =
        new[] { typeof(DenseVegetation), typeof(MorphicGrove), typeof(Neow) };

    public AncientPoolTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Overgrowth_AncientPool_IsNeowOnly()
    {
        Assert.Equal(new[] { typeof(Neow) }, new Overgrowth().AncientPool);
    }

    [Fact]
    public async Task Overgrowth_AncientRoom_StillCreatesNeow()
    {
        RunState runState = CreateRunState("ancient-overgrowth", new Overgrowth());
        _ = runState.CurrentAncientEventType;
        int upFrontCounterBeforeRoomAccess = runState.Rng.UpFront.Counter;

        var room = Assert.IsType<EventRoom>(RoomFactory.CreateAncientEventRoom(runState));
        await room.Enter(runState);

        Assert.IsType<Neow>(room.Event);
        Assert.Equal(upFrontCounterBeforeRoomAccess, runState.Rng.UpFront.Counter);
    }

    [Fact]
    public void MultiAncientAct_PicksOneByUpFrontRng()
    {
        const string seed = "ancient-upfront";
        RunState runState = CreateRunState(seed, new TestAct(0, MultiAncientPool));
        Type expected = SelectAncientWithAuthoritativeUpFrontOrder(
            runState.Rng.UpFront.CloneExact(), MultiAncientPool, out int expectedCounter);
        _ = runState.CurrentAncientEventType;
        int counterAfterActInitialization = runState.Rng.UpFront.Counter;
        var room = Assert.IsType<EventRoom>(RoomFactory.CreateAncientEventRoom(runState));

        Assert.Equal(expected, runState.CurrentAncientEventType);
        Assert.Contains(runState.CurrentAncientEventType, MultiAncientPool);
        Assert.Equal(expectedCounter, counterAfterActInitialization);
        Assert.Equal(counterAfterActInitialization, runState.Rng.UpFront.Counter);
        Assert.NotNull(room);
    }

    [Fact]
    public void MultiAncientAct_SameSeed_PicksSame()
    {
        RunState first = CreateRunState("ancient-deterministic", new TestAct(0, MultiAncientPool));
        RunState second = CreateRunState("ancient-deterministic", new TestAct(0, MultiAncientPool));

        Assert.Equal(first.CurrentAncientEventType, second.CurrentAncientEventType);
        Assert.Equal(first.Rng.UpFront.Counter, second.Rng.UpFront.Counter);
    }

    [Fact]
    public async Task AncientRoom_UsesCurrentActPool()
    {
        var firstPool = new[] { typeof(Neow) };
        var secondPool = new[] { typeof(DenseVegetation) };
        RunState runState = CreateRunState(
            "ancient-switch-act",
            new TestAct(0, firstPool),
            new TestAct(1, secondPool));

        Type firstSelection = runState.CurrentAncientEventType;
        runState.AdvanceToNextAct();
        Type secondSelection = runState.CurrentAncientEventType;
        int counterBeforeRoomAccess = runState.Rng.UpFront.Counter;
        var room = Assert.IsType<EventRoom>(RoomFactory.CreateAncientEventRoom(runState));
        await room.Enter(runState);

        Assert.Equal(typeof(Neow), firstSelection);
        Assert.Equal(typeof(DenseVegetation), secondSelection);
        Assert.IsType<DenseVegetation>(room.Event);
        Assert.Equal(counterBeforeRoomAccess, runState.Rng.UpFront.Counter);
    }

    [Fact]
    public void EmptyAncientPool_FailsDuringActInitialization()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () =>
            {
                RunState runState = CreateRunState("ancient-empty", new TestAct(0, Array.Empty<Type>()));
                _ = runState.CurrentAncientEventType;
            });

        Assert.Contains("Ancient", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pool", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RunState CreateRunState(string seed, params ActDefinition[] acts)
    {
        var runState = new RunState(seed, acts);
        runState.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Regent>(), runState));
        return runState;
    }

    private static Type SelectAncientWithAuthoritativeUpFrontOrder(
        Rng rng,
        IReadOnlyList<Type> ancientPool,
        out int counter)
    {
        var act = new TestAct(0, ancientPool);
        // Shared events now participate in the same single shuffle before Ancient selection.
        var events = act.EventPool.Concat(SharedEventPool.All).ToList();
        rng.Shuffle(events);

        for (int i = 0; i < act.BaseNumberOfRooms; i++)
        {
            _ = act.PickEncounter(RoomType.Monster, rng);
        }

        for (int i = 0; i < 15; i++)
        {
            _ = act.PickEncounter(RoomType.Elite, rng);
        }

        _ = act.PickEncounter(RoomType.Boss, rng);
        Type selected = rng.NextItem(ancientPool)!;
        counter = rng.Counter;
        return selected;
    }

    private sealed class TestAct : ActDefinition
    {
        private static readonly IReadOnlyList<EncounterDefinition> Encounters =
            new[]
            {
                new EncounterDefinition(
                    (Func<MonsterModel>)(() => throw new NotSupportedException("Selection tests do not create monsters."))),
            };

        public TestAct(int index, IReadOnlyList<Type> ancientPool)
        {
            Index = index;
            AncientPool = ancientPool;
        }

        public override int Index { get; }

        public override IReadOnlyList<Type> EventPool => Array.Empty<Type>();

        public override IReadOnlyList<Type> AncientPool { get; }

        public override int BaseNumberOfRooms => 15;

        public override int NumberOfWeakEncounters => 0;

        protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => Encounters;

        protected override IReadOnlyList<EncounterDefinition> EliteEncounters => Encounters;

        protected override IReadOnlyList<EncounterDefinition> BossEncounters => Encounters;

        public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);
    }
}
