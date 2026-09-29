namespace Sts2Sim.Core.Tests.Hooks;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")] // 创建 AbstractModel 子类实例(构造器读 ModelDb),须与注册表变更测试串行
public class HookDispatchTests
{
    private sealed class FakeRunState(params AbstractModel[] listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;

        public RunRngSet Rng { get; } = new RunRngSet("hook_dispatch_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    /// <summary>可编程 fake 监听器:用委托配置每个 hook 的行为,并记录调用。</summary>
    private sealed class ProbeModel : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;

        public int Invocations { get; private set; }

        public Func<IReadOnlySet<RoomType>, IReadOnlySet<RoomType>>? OnModifyRoomTypes { get; init; }

        public Func<float, float>? OnModifyOddsIncrease { get; init; }

        public bool ForcePotion { get; init; }

        public bool AllowProceed { get; init; } = true;

        public bool AllowFreeTravel { get; init; }

        public int AfterRoomEnteredCallCount { get; private set; }

        public List<string>? CallLog { get; init; }

        public string Name { get; init; } = "probe";

        public override Task AfterActEntered()
        {
            Invocations++;
            CallLog?.Add(Name);
            return Task.CompletedTask;
        }

        public override Task AfterRoomEntered(AbstractRoom room)
        {
            AfterRoomEnteredCallCount++;
            return Task.CompletedTask;
        }

        public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes)
        {
            Invocations++;
            CallLog?.Add(Name);
            return OnModifyRoomTypes?.Invoke(roomTypes) ?? roomTypes;
        }

        public override float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float oddsIncrease)
        {
            Invocations++;
            CallLog?.Add(Name);
            return OnModifyOddsIncrease?.Invoke(oddsIncrease) ?? oddsIncrease;
        }

        public override bool ShouldForcePotionReward(RoomType roomType)
        {
            Invocations++;
            return ForcePotion;
        }

        public override bool ShouldProceedToNextMapPoint()
        {
            Invocations++;
            return AllowProceed;
        }

        public override bool ShouldAllowFreeTravel()
        {
            Invocations++;
            return AllowFreeTravel;
        }
    }

    private sealed class FakeRoomForHookTest : AbstractRoom
    {
        public override RoomType RoomType => RoomType.Event;

        public override ModelId? ModelId => null;

        public int EnterInternalCallCount { get; private set; }

        public override Task EnterInternal(RunState? runState)
        {
            EnterInternalCallCount++;
            return Task.CompletedTask;
        }

        public override Task Exit(RunState? runState) => Task.CompletedTask;
    }

    [Fact]
    public async Task AfterRoomEntered_InvokesEveryListener()
    {
        var listener = new ProbeModel();
        var runState = new FakeRunState(listener);
        var room = new FakeRoomForHookTest();

        await Hook.AfterRoomEntered(runState, room);

        Assert.Equal(1, listener.AfterRoomEnteredCallCount);
    }

    [Fact]
    public async Task AbstractRoomEnter_CompletesRealEntryPathBeforeAfterRoomEnteredDispatch()
    {
        var runState = new RunState("after-room-entered-production-path", new Overgrowth());
        var room = new FakeRoomForHookTest();

        await room.Enter(runState);

        Assert.Equal(0, room.Id);
        Assert.Equal(1, room.EnterInternalCallCount);
    }

    [Fact]
    public async Task AfterActEntered_InvokesListenersInOrder_AndFiresExecutionFinished()
    {
        var log = new List<string>();
        var a = new ProbeModel { Name = "a", CallLog = log };
        var b = new ProbeModel { Name = "b", CallLog = log };
        int finished = 0;
        a.ExecutionFinished += _ => finished++;
        b.ExecutionFinished += _ => finished++;

        await Hook.AfterActEntered(new FakeRunState(a, b));

        Assert.Equal(new[] { "a", "b" }, log);
        Assert.Equal(2, finished);
    }

    [Fact]
    public void ModifyUnknownMapPointRoomTypes_FoldsInOrder_AndDoesNotMutateInput()
    {
        var original = new HashSet<RoomType> { RoomType.Monster, RoomType.Shop, RoomType.Event };
        var removeMonster = new ProbeModel
        {
            OnModifyRoomTypes = set => set.Where(t => t != RoomType.Monster).ToHashSet(),
        };
        var removeShop = new ProbeModel
        {
            OnModifyRoomTypes = set => set.Where(t => t != RoomType.Shop).ToHashSet(),
        };

        IReadOnlySet<RoomType> result =
            Hook.ModifyUnknownMapPointRoomTypes(new FakeRunState(removeMonster, removeShop), original);

        Assert.Equal(new HashSet<RoomType> { RoomType.Event }, result);
        // 调度器先复制集合再折叠:原集合不被修改
        Assert.Equal(3, original.Count);
    }

    [Fact]
    public void ModifyUnknownMapPointRoomTypes_CopiesInput_EvenWhenListenerMutatesInPlace()
    {
        var original = new HashSet<RoomType> { RoomType.Monster, RoomType.Event };
        var inPlaceMutator = new ProbeModel
        {
            OnModifyRoomTypes = set =>
            {
                // 恶意监听器:原地修改传入的集合
                ((HashSet<RoomType>)set).Remove(RoomType.Monster);
                return set;
            },
        };

        IReadOnlySet<RoomType> result =
            Hook.ModifyUnknownMapPointRoomTypes(new FakeRunState(inPlaceMutator), original);

        Assert.Equal(new HashSet<RoomType> { RoomType.Event }, result);
        // 调度器把入参复制成新 HashSet 后才交给监听器:调用方的原集合必须不受原地修改影响
        Assert.Contains(RoomType.Monster, original);
        Assert.Equal(2, original.Count);
    }

    [Fact]
    public void ModifyOddsIncreaseForUnrolledRoomType_ChainsThroughListeners()
    {
        var doubler = new ProbeModel { OnModifyOddsIncrease = v => v * 2f };
        var addFive = new ProbeModel { OnModifyOddsIncrease = v => v + 5f };

        float result = Hook.ModifyOddsIncreaseForUnrolledRoomType(
            new FakeRunState(doubler, addFive), RoomType.Monster, 0.1f);

        // (0.1 * 2) + 5 —— 顺序敏感
        Assert.Equal(5.2f, result, 5);
    }

    [Fact]
    public void ShouldForcePotionReward_OrFold_ShortCircuitsLaterCallsButFinishesLoop()
    {
        var no = new ProbeModel { ForcePotion = false };
        var yes = new ProbeModel { ForcePotion = true };
        var after = new ProbeModel { ForcePotion = false };

        bool result = Hook.ShouldForcePotionReward(new FakeRunState(no, yes, after), RoomType.Monster);

        Assert.True(result);
        Assert.Equal(1, no.Invocations);
        Assert.Equal(1, yes.Invocations);
        // 游戏语义:flag = flag || item.X() —— flag 为 true 后,后续监听器的方法因 || 短路不再被调用
        Assert.Equal(0, after.Invocations);
    }

    [Fact]
    public void ShouldProceedToNextMapPoint_AndVeto_ReturnsEarlyOnFalse()
    {
        var allow = new ProbeModel { AllowProceed = true };
        var veto = new ProbeModel { AllowProceed = false };
        var after = new ProbeModel { AllowProceed = true };

        bool result = Hook.ShouldProceedToNextMapPoint(new FakeRunState(allow, veto, after));

        Assert.False(result);
        Assert.Equal(1, allow.Invocations);
        Assert.Equal(1, veto.Invocations);
        // 游戏语义:遇 false 立即 return —— 后续监听器不再被调用
        Assert.Equal(0, after.Invocations);
    }

    [Fact]
    public void ShouldAllowFreeTravel_DefaultsFalse_TrueIfAnyListenerAllows()
    {
        Assert.False(Hook.ShouldAllowFreeTravel(new FakeRunState()));
        Assert.False(Hook.ShouldAllowFreeTravel(new FakeRunState(new ProbeModel())));
        Assert.True(Hook.ShouldAllowFreeTravel(new FakeRunState(new ProbeModel { AllowFreeTravel = true })));
    }

    [Fact]
    public void EmptyListenerList_AllHooksReturnNeutralResults()
    {
        var runState = new FakeRunState();
        var set = new HashSet<RoomType> { RoomType.Event };
        Assert.Equal(set, Hook.ModifyUnknownMapPointRoomTypes(runState, set));
        Assert.Equal(0.1f, Hook.ModifyOddsIncreaseForUnrolledRoomType(runState, RoomType.Shop, 0.1f));
        Assert.False(Hook.ShouldForcePotionReward(runState, RoomType.Elite));
        Assert.True(Hook.ShouldProceedToNextMapPoint(runState));
    }
}
