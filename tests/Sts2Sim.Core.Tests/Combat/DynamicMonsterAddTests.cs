using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Combat;

[Collection("ModelDb")]
public sealed class DynamicMonsterAddTests : IDisposable
{
    public DynamicMonsterAddTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(LifecycleMonster),
            typeof(EngineLifecycleMonster),
            typeof(SummonerMonster),
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
    public async Task Add_RegistersAndInitializesMonsterOnce_WithoutReplayingCombatStart()
    {
        var runState = new FakeRunState();
        ICombatState combatState = new CombatState(runState);
        var existing = (LifecycleMonster)ModelDb.Monster<LifecycleMonster>().MutableClone();
        Creature existingCreature = combatState.AddMonster(existing, CombatSide.Enemy, "existing");
        existing.SetUpForCombat();
        existing.RollMove(combatState.Allies);
        await Hook.BeforeCombatStart(combatState);
        int existingHp = existingCreature.CurrentHp;
        uint? existingCombatId = existingCreature.CombatId;
        var added = (LifecycleMonster)ModelDb.Monster<LifecycleMonster>().MutableClone();

        Creature addedCreature = await CreatureCmd.Add(added, combatState, CombatSide.Enemy, "illusion");

        Assert.Equal(new[] { existingCreature, addedCreature }, combatState.Enemies);
        Assert.Equal(existingHp, existingCreature.CurrentHp);
        Assert.Equal(existingCombatId, existingCreature.CombatId);
        Assert.Equal("existing", existingCreature.SlotName);
        Assert.Same(added, addedCreature.Monster);
        Assert.Same(addedCreature, added.Creature);
        Assert.Same(combatState, addedCreature.CombatState);
        Assert.Equal(CombatSide.Enemy, addedCreature.Side);
        Assert.Equal("illusion", addedCreature.SlotName);
        Assert.Equal(1u, addedCreature.CombatId);
        Assert.Equal(1, added.SetupCount);
        Assert.Equal(1, added.AfterAddedToRoomCount);
        Assert.Equal(1, added.InitialRollCount);
        Assert.Equal(0, added.BeforeCombatStartCount);
        Assert.Equal(new[] { "setup", "after-added", "roll" }, added.LifecycleEvents);
        Assert.Equal(1, existing.SetupCount);
        Assert.Equal(0, existing.AfterAddedToRoomCount);
        Assert.Equal(1, existing.InitialRollCount);
        Assert.Equal(1, existing.BeforeCombatStartCount);
        Assert.Equal(new[] { "setup", "roll", "before-combat-start" }, existing.LifecycleEvents);
        Assert.True(added.SpawnedThisTurn);
        Assert.NotNull(added.MoveStateMachine);
        Assert.NotNull(added.NextMove);

        await added.PerformMove();

        Assert.Equal(1, added.PerformedMoveCount);
    }

    [Fact]
    public async Task Add_DuringPlayerTurn_NewMonsterActsOnceThroughEngineAndTargetsPlayer()
    {
        var runState = new RunState("dynamic-monster-engine", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var existing = (EngineLifecycleMonster)ModelDb.Monster<EngineLifecycleMonster>().MutableClone();
        var room = new CombatRoom(() => existing);
        runState.PushRoom(room);
        await room.Enter(runState);
        Creature existingCreature = Assert.Single(room.Engine.State.Enemies);
        int existingHp = existingCreature.CurrentHp;
        uint? existingCombatId = existingCreature.CombatId;
        string? existingSlot = existingCreature.SlotName;
        int existingBeforeCombatStartCount = existing.BeforeCombatStartCount;
        var added = (EngineLifecycleMonster)ModelDb.Monster<EngineLifecycleMonster>().MutableClone();
        added.DamagePerMove = 3;
        int playerHpBefore = player.Creature.CurrentHp;

        Assert.NotSame(existing.LifecycleEvents, added.LifecycleEvents);

        Creature addedCreature = await CreatureCmd.Add(
            added,
            room.Engine.State,
            CombatSide.Enemy,
            "illusion");
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(playerHpBefore - 3, player.Creature.CurrentHp);
        Assert.Equal(1, added.PerformedMoveCount);
        Assert.Same(player.Creature, added.LastTarget);
        Assert.Equal(1, added.SetupCount);
        Assert.Equal(1, added.AfterAddedToRoomCount);
        Assert.Equal(1, added.InitialRollCount);
        Assert.Equal(0, added.BeforeCombatStartCount);
        Assert.Equal(new[] { "setup", "after-added", "roll" }, added.LifecycleEvents);
        Assert.Equal("illusion", addedCreature.SlotName);
        Assert.Same(existingCreature, room.Engine.State.Enemies[0]);
        Assert.Equal(existingHp, existingCreature.CurrentHp);
        Assert.Equal(existingCombatId, existingCreature.CombatId);
        Assert.Equal(existingSlot, existingCreature.SlotName);
        Assert.Equal(existingBeforeCombatStartCount, existing.BeforeCombatStartCount);
        Assert.Equal(1, existing.SetupCount);
        Assert.Equal(1, existing.AfterAddedToRoomCount);
        Assert.Equal(1, existing.InitialRollCount);
        Assert.Equal(1, existing.BeforeCombatStartCount);
        Assert.Equal(new[] { "setup", "after-added", "before-combat-start", "roll" }, existing.LifecycleEvents);
    }

    // 原版 CombatManager.AfterCreatureAdded 只在玩家回合给新加入的敌人当场掷招；敌方回合召唤的怪物本回合不行动
    // （Creature.TakeTurn 跳过 SpawnedThisTurn），第一招在下个玩家回合开始时和其他敌人一起掷。
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Add_RollsFirstMoveOnlyWhenAddedDuringPlayerTurn(bool summonDuringEnemyTurn)
    {
        var runState = new RunState("dynamic-monster-summon-timing", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var summoner = (SummonerMonster)ModelDb.Monster<SummonerMonster>().MutableClone();
        summoner.SummonOnMove = summonDuringEnemyTurn;
        var room = new CombatRoom(() => summoner);
        runState.PushRoom(room);
        await room.Enter(runState);

        EngineLifecycleMonster added;
        if (summonDuringEnemyTurn)
        {
            await room.Engine.EndPlayerTurnAsync();
            added = Assert.IsType<EngineLifecycleMonster>(summoner.Summoned);
            Assert.Equal(0, summoner.SummonedRollCountAtSummon);
            Assert.True(summoner.SummonedHadNoMoveAtSummon);
            Assert.Equal(0, added.PerformedMoveCount);
        }
        else
        {
            added = (EngineLifecycleMonster)ModelDb.Monster<EngineLifecycleMonster>().MutableClone();
            await CreatureCmd.Add(added, room.Engine.State, CombatSide.Enemy, "illusion");
            Assert.Equal(1, added.InitialRollCount);
            await room.Engine.EndPlayerTurnAsync();
            Assert.Equal(1, added.PerformedMoveCount);
        }

        // 两种情况下第一招都只掷过一次：玩家回合召唤的在加入时掷，敌方回合召唤的在随后的玩家回合开始时掷。
        Assert.Equal(1, added.InitialRollCount);
        Assert.Equal(new[] { "setup", "after-added", "roll" }, added.LifecycleEvents);
        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        Assert.NotNull(added.NextMove);
        Assert.False(added.SpawnedThisTurn);
    }

    private sealed class FakeRunState : IRunState
    {
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? Array.Empty<AbstractModel>();

        public RunRngSet Rng { get; } = new("dynamic-monster-add");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
    }

    private sealed class LifecycleMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 10;

        public int SetupCount { get; private set; }

        public int AfterAddedToRoomCount { get; private set; }

        public int InitialRollCount { get; private set; }

        public int BeforeCombatStartCount { get; private set; }

        public int PerformedMoveCount { get; private set; }

        public List<string> LifecycleEvents { get; private set; } = new();

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            SetupCount++;
            LifecycleEvents.Add("setup");
            var move = new MoveState(
                "TEST_MOVE",
                _ =>
                {
                    PerformedMoveCount++;
                    return Task.CompletedTask;
                },
                new BuffIntent());
            move.FollowUpState = move;
            var initial = new CountingInitialState("INIT", move.Id, () =>
            {
                InitialRollCount++;
                LifecycleEvents.Add("roll");
            });
            return new MonsterMoveStateMachine(new MonsterState[] { initial, move }, initial);
        }

        public override Task AfterAddedToRoom()
        {
            AfterAddedToRoomCount++;
            LifecycleEvents.Add("after-added");
            return Task.CompletedTask;
        }

        public override Task BeforeCombatStart()
        {
            BeforeCombatStartCount++;
            LifecycleEvents.Add("before-combat-start");
            return Task.CompletedTask;
        }

        protected override void DeepCloneFields()
        {
            base.DeepCloneFields();
            LifecycleEvents = new List<string>(LifecycleEvents);
        }

        protected override void AfterCloned()
        {
            base.AfterCloned();
            SetupCount = 0;
            AfterAddedToRoomCount = 0;
            InitialRollCount = 0;
            BeforeCombatStartCount = 0;
            PerformedMoveCount = 0;
            LifecycleEvents.Clear();
        }
    }

    private sealed class EngineLifecycleMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 10;

        public int DamagePerMove { get; set; }

        public int SetupCount { get; private set; }

        public int AfterAddedToRoomCount { get; private set; }

        public int InitialRollCount { get; private set; }

        public int BeforeCombatStartCount { get; private set; }

        public int PerformedMoveCount { get; private set; }

        public Creature? LastTarget { get; private set; }

        public List<string> LifecycleEvents { get; private set; } = new();

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            SetupCount++;
            LifecycleEvents.Add("setup");
            var move = new MoveState(
                "ENGINE_TEST_MOVE",
                async targets =>
                {
                    PerformedMoveCount++;
                    LastTarget = targets.Single();
                    await CreatureCmd.Damage(
                        Creature.CombatState!,
                        new[] { LastTarget },
                        DamagePerMove,
                        ValueProp.Move,
                        Creature,
                        null,
                        null);
                },
                new SingleAttackIntent(3));
            move.FollowUpState = move;
            var initial = new CountingInitialState("INIT", move.Id, () =>
            {
                InitialRollCount++;
                LifecycleEvents.Add("roll");
            });
            return new MonsterMoveStateMachine(new MonsterState[] { initial, move }, initial);
        }

        public override Task AfterAddedToRoom()
        {
            AfterAddedToRoomCount++;
            LifecycleEvents.Add("after-added");
            return Task.CompletedTask;
        }

        public override Task BeforeCombatStart()
        {
            BeforeCombatStartCount++;
            LifecycleEvents.Add("before-combat-start");
            return Task.CompletedTask;
        }

        protected override void DeepCloneFields()
        {
            base.DeepCloneFields();
            LifecycleEvents = new List<string>(LifecycleEvents);
        }

        protected override void AfterCloned()
        {
            base.AfterCloned();
            DamagePerMove = 0;
            SetupCount = 0;
            AfterAddedToRoomCount = 0;
            InitialRollCount = 0;
            BeforeCombatStartCount = 0;
            PerformedMoveCount = 0;
            LastTarget = null;
            LifecycleEvents.Clear();
        }
    }

    private sealed class SummonerMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 10;

        public bool SummonOnMove { get; set; }

        public EngineLifecycleMonster? Summoned { get; private set; }

        public int SummonedRollCountAtSummon { get; private set; } = -1;

        public bool SummonedHadNoMoveAtSummon { get; private set; }

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var move = new MoveState(
                "SUMMON_TEST_MOVE",
                async _ =>
                {
                    if (!SummonOnMove || Summoned is not null)
                    {
                        return;
                    }

                    var summoned = (EngineLifecycleMonster)ModelDb.Monster<EngineLifecycleMonster>().MutableClone();
                    await CreatureCmd.Add(summoned, Creature.CombatState!, CombatSide.Enemy, "summoned");
                    Summoned = summoned;
                    SummonedRollCountAtSummon = summoned.InitialRollCount;
                    SummonedHadNoMoveAtSummon = summoned.NextMove is null;
                },
                new BuffIntent());
            move.FollowUpState = move;
            return new MonsterMoveStateMachine(new MonsterState[] { move }, move);
        }

        protected override void AfterCloned()
        {
            base.AfterCloned();
            SummonOnMove = false;
            Summoned = null;
            SummonedRollCountAtSummon = -1;
            SummonedHadNoMoveAtSummon = false;
        }
    }

    private sealed class CountingInitialState(
        string id,
        string targetStateId,
        Action onTransition) : MonsterState
    {
        public override string Id { get; } = id;

        public override bool IsMove => false;

        public override bool ShouldAppearInLogs => false;

        public override string GetNextState(
            Creature owner,
            Sts2Sim.Core.Random.Rng rng)
        {
            onTransition();
            return targetStateId;
        }

        public override void RegisterStates(Dictionary<string, MonsterState> monsterStates) =>
            monsterStates.Add(Id, this);
    }
}
