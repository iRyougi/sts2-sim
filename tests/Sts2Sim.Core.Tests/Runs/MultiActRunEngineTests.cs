using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Runs;

public sealed class MultiActRunEngineTests : IDisposable
{
    public MultiActRunEngineTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes.Append(typeof(SelfDefeatingBossMonster)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunEngine_SingleAct_BossStillCompletesRun()
    {
        (RunState runState, _) = CreateRunState(includeSecondAct: false);

        var engine = new RunEngine(runState, PickFirstByCoord);
        RunEngine.Result result = await engine.RunAsync(maxFloors: 1);

        Assert.True(result.Won);
        Assert.True(result.ReachedBoss);
        Assert.Equal(0, runState.CurrentActIndex);
    }

    [Fact]
    public async Task RunEngine_TwoActs_BossAdvancesInsteadOfCompleting()
    {
        (RunState runState, _) = CreateRunState(includeSecondAct: true);
        var ancientFactoryActIndices = new List<int>();
        var engine = new RunEngine(
            runState,
            PickFirstByCoord,
            createAncientEventRoom: state =>
            {
                ancientFactoryActIndices.Add(state.CurrentActIndex);
                return CreateNoopAncientRoom();
            });

        RunEngine.Result result = await engine.RunAsync(maxFloors: 1);

        Assert.False(result.Won);
        Assert.Equal(1, runState.CurrentActIndex);
        Assert.Equal([1], ancientFactoryActIndices);
    }

    [Fact]
    public async Task RunEngine_TwoActs_FinalBossCompletesRun()
    {
        (RunState runState, _) = CreateRunState(includeSecondAct: true);
        var engine = new RunEngine(
            runState,
            points =>
            {
                MapPoint chosen = PickFirstByCoord(points);
                if (runState.CurrentActIndex == 1)
                {
                    chosen.PointType = MapPointType.Boss;
                }

                return chosen;
            },
            createAncientEventRoom: _ => CreateNoopAncientRoom());

        RunEngine.Result result = await engine.RunAsync(maxFloors: 2);

        Assert.True(result.Won);
        Assert.True(result.ReachedBoss);
        Assert.Equal(1, runState.CurrentActIndex);
        Assert.Equal(2, result.FloorsVisited);
    }

    [Fact]
    public async Task RunDriver_TwoActs_ActOneBossContinuesIntoHiveMapDecision()
    {
        var runState = new RunState(
            "multi-act-run-driver",
            new ActDefinition[] { new MultiActTestAct(0), new Hive() });
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Boss;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var mapDecisionActIndices = new List<int>();
        var ancientFactoryActIndices = new List<int>();
        var decisions = new MultiActDriverDecisionSource(runState, mapDecisionActIndices);
        var driver = new RunDriver(
            runState,
            decisions,
            createAncientEventRoom: state =>
            {
                ancientFactoryActIndices.Add(state.CurrentActIndex);
                return CreateNoopAncientRoom();
            });

        RunDriver.Result result = await driver.RunAsync(maxFloors: 2);

        Assert.False(result.Won);
        Assert.Equal(2, result.FloorsVisited);
        Assert.Equal(1, runState.CurrentActIndex);
        Assert.IsType<Hive>(runState.Act);
        Assert.Equal([0, 1], mapDecisionActIndices);
        Assert.Equal([1], ancientFactoryActIndices);
        Assert.Equal(2, runState.VisitedMapCoords.Count);
    }

    [Fact]
    public async Task RunEngine_DeathInSecondAct_ReportsCorrectFloors()
    {
        (RunState runState, Player player) = CreateRunState(includeSecondAct: true);
        var recorder = new RunRecorder(() => DateTimeOffset.UnixEpoch);
        var engine = new RunEngine(
            runState,
            points =>
            {
                MapPoint chosen = PickFirstByCoord(points);
                if (runState.CurrentActIndex == 1)
                {
                    chosen.PointType = MapPointType.Monster;
                }

                return chosen;
            },
            createAncientEventRoom: _ => CreateNoopAncientRoom(),
            recorder: recorder);
        engine.OnRoomResolved += (_, _) =>
        {
            if (runState.CurrentActIndex == 1)
            {
                player.Creature.LoseHpInternal(player.Creature.CurrentHp, default(ValueProp));
            }
        };

        RunEngine.Result result = await engine.RunAsync(maxFloors: 2);

        Assert.False(result.Won);
        Assert.True(runState.IsGameOver);
        Assert.Equal(2, result.FloorsVisited);
        Assert.Equal(3, recorder.BuildManifest().FloorsVisited);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public async Task RunEngine_DeathInLaterAct_ReportsClearedActs(int actCount, int expectedActsCleared)
    {
        (RunState runState, Player player) = CreateRunState(actCount);
        var engine = new RunEngine(runState, points =>
        {
            MapPoint chosen = PickFirstByCoord(points);
            chosen.PointType = runState.CurrentActIndex < actCount - 1 ? MapPointType.Boss : MapPointType.Monster;
            return chosen;
        }, createAncientEventRoom: _ => CreateNoopAncientRoom());
        engine.OnRoomResolved += (_, _) =>
        {
            if (runState.CurrentActIndex == actCount - 1)
            {
                player.Creature.LoseHpInternal(player.Creature.CurrentHp, default(ValueProp));
            }
        };

        RunEngine.Result result = await engine.RunAsync(maxFloors: actCount);

        Assert.False(result.Won);
        Assert.True(runState.IsGameOver);
        Assert.Equal(expectedActsCleared, result.ActsCleared);
    }
    /// <summary>打通 Act1 Boss 后被 maxFloors 截断：没通关，但确实打到过 Boss 且玩家还活着。
    /// 这三件事必须能分开表达，否则 08b-5 的 CMA-ES 会把"进到第二幕才死"和
    /// "连第一幕 Boss 都没摸到"混为一谈。</summary>
    [Fact]
    public async Task RunEngine_TwoActs_ClearedFirstBossThenTruncated_SeparatesReachedFromWon()
    {
        (RunState runState, _) = CreateRunState(includeSecondAct: true);
        var engine = new RunEngine(
            runState,
            PickFirstByCoord,
            createAncientEventRoom: _ => CreateNoopAncientRoom());

        RunEngine.Result result = await engine.RunAsync(maxFloors: 1);

        Assert.False(result.Won);
        Assert.True(result.ReachedBoss);
        Assert.True(result.Survived);
        Assert.True(result.Truncated);
        Assert.Equal(1, result.ActsCleared);
    }

    [Fact]
    public async Task RunDriver_TwoActs_ClearedFirstBossThenTruncated_SeparatesReachedFromWon()
    {
        var runState = new RunState(
            "multi-act-result-semantics",
            new ActDefinition[] { new MultiActTestAct(0), new MultiActTestAct(1) });
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Boss;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var driver = new RunDriver(
            runState,
            new MultiActDriverDecisionSource(runState, []),
            createAncientEventRoom: _ => CreateNoopAncientRoom());

        RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

        Assert.False(result.Won);
        Assert.True(result.ReachedBoss);
        Assert.True(result.Survived);
        Assert.True(result.Truncated);
        Assert.Equal(1, result.ActsCleared);
    }

    /// <summary>打通最终幕 Boss：通关，且不算截断。</summary>
    [Fact]
    public async Task RunEngine_SingleAct_FinalBossWin_IsNotTruncated()
    {
        (RunState runState, _) = CreateRunState(includeSecondAct: false);

        RunEngine.Result result = await new RunEngine(runState, PickFirstByCoord).RunAsync(maxFloors: 1);

        Assert.True(result.Won);
        Assert.True(result.ReachedBoss);
        Assert.True(result.Survived);
        Assert.False(result.Truncated);
        Assert.Equal(1, result.ActsCleared);
    }

    private static (RunState RunState, Player Player) CreateRunState(bool includeSecondAct)
    {
        ActDefinition[] acts = includeSecondAct
            ? [new MultiActTestAct(0), new MultiActTestAct(1)]
            : [new MultiActTestAct(0)];
        var runState = new RunState("multi-act-run-engine", acts);
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Boss;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static (RunState RunState, Player Player) CreateRunState(int actCount)
    {
        ActDefinition[] acts = Enumerable.Range(0, actCount)
            .Select(index => (ActDefinition)new MultiActTestAct(index))
            .ToArray();
        var runState = new RunState("multi-act-later-act-death", acts);
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Boss;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
    private static MapPoint PickFirstByCoord(IEnumerable<MapPoint> points) =>
        points.OrderBy(point => point.coord.col).First();

    private sealed class SelfDefeatingBossMonster : MonsterModel
    {
        public override int MinInitialHp => 1;

        public override int MaxInitialHp => 1;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var expire = new MoveState(
                "EXPIRE",
                async _ => await CreatureCmd.LoseHp(
                    Creature.CombatState!.RunState,
                    Creature,
                    Creature.CurrentHp,
                    ValueProp.Unblockable | ValueProp.Unpowered),
                new DefendIntent());
            expire.FollowUpState = expire;
            return new MonsterMoveStateMachine([expire], expire);
        }
    }

    private sealed class MultiActTestAct(int index) : ActDefinition
    {
        private static readonly IReadOnlyList<EncounterDefinition> Encounters =
        [
            new EncounterDefinition(
                () => (MonsterModel)ModelDb.Monster<SelfDefeatingBossMonster>().MutableClone(),
                tags: null,
                isWeak: false,
                name: "multi-act-engine-self-defeating"),
        ];

        public override int Index => index;

        public override IReadOnlyList<Type> EventPool => Array.Empty<Type>();
        public override IReadOnlyList<Type> AncientPool => [typeof(Sts2Sim.Core.Models.Events.Neow)];

        public override int BaseNumberOfRooms => 15;

        public override int NumberOfWeakEncounters => 0;

        protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => Encounters;

        protected override IReadOnlyList<EncounterDefinition> EliteEncounters => Encounters;

        protected override IReadOnlyList<EncounterDefinition> BossEncounters => Encounters;

        public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);
    }

    private sealed class MultiActDriverDecisionSource(
        RunState runState,
        List<int> mapDecisionActIndices) : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options)
        {
            mapDecisionActIndices.Add(runState.CurrentActIndex);
            MapPoint chosen = PickFirstByCoord(options);
            if (runState.CurrentActIndex == 1)
            {
                chosen.PointType = MapPointType.Treasure;
            }

            return Task.FromResult(chosen);
        }

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
            Task.FromResult(options[0]);

        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
            Task.FromResult(RewardDecisionPolicy.Choose(rewards));
    }

    private static EventRoom CreateNoopAncientRoom() =>
        new(() => (EventModel)new NoopAncientEvent().MutableClone());

    private sealed class NoopAncientEvent : EventModel
    {
        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [
            new EventOption("DONE", () =>
            {
                Finish();
                return Task.CompletedTask;
            }),
        ];
    }
}
