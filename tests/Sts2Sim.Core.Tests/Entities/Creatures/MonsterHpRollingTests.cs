using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Creatures;

[Collection("ModelDb")]
public sealed class MonsterHpRollingTests : IDisposable
{
    public MonsterHpRollingTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(WideRangeMonster),
            typeof(OtherWideRangeMonster),
            typeof(NarrowRangeMonster),
            typeof(FixedHpMonster),
            typeof(FixedElevenHpMonster),
            typeof(NonNegativeIntRangeMonster),
            typeof(IntMaxRangeMonster),
            typeof(FullIntRangeMonster),
            typeof(InvalidRangeMonster),
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void AddMonster_SingleMonsterWithFixedSeed_RollsBelowMaximumOnNicheStream()
    {
        var runState = new FakeRunState("monster-hp-single");
        var combatState = new CombatState(runState);

        Creature creature = combatState.AddMonster(Clone<WideRangeMonster>(), CombatSide.Enemy);

        Assert.InRange(creature.MaxHp, 10, 14);
        Assert.Equal(creature.MaxHp, creature.CurrentHp);
        Assert.NotEqual(14, creature.MaxHp);
        Assert.Equal(1, runState.Rng.Niche.Counter);
        Assert.Equal(0, runState.Rng.MonsterAi.Counter);
        Assert.Equal(0, creature.Monster!.Rng.Counter);
    }

    [Fact]
    public void AddMonster_SameAndDifferentMonsterTypes_UseDistinctHpWhenRangeHasCapacity()
    {
        var combatState = new CombatState(new FakeRunState("monster-hp-unique"));

        Creature[] creatures =
        {
            combatState.AddMonster(Clone<WideRangeMonster>(), CombatSide.Enemy),
            combatState.AddMonster(Clone<WideRangeMonster>(), CombatSide.Enemy),
            combatState.AddMonster(Clone<OtherWideRangeMonster>(), CombatSide.Enemy),
            combatState.AddMonster(Clone<OtherWideRangeMonster>(), CombatSide.Enemy),
        };

        Assert.All(creatures, creature => Assert.InRange(creature.MaxHp, 10, 14));
        Assert.Equal(creatures.Length, creatures.Select(creature => creature.MaxHp).Distinct().Count());
    }

    [Fact]
    public async Task AddMonster_WhenEveryCandidateIsUsed_FallsBackWithinRangeWithoutLooping()
    {
        var combatState = new CombatState(new FakeRunState("monster-hp-exhausted"));
        Creature first = combatState.AddMonster(Clone<NarrowRangeMonster>(), CombatSide.Enemy);
        Creature second = combatState.AddMonster(Clone<NarrowRangeMonster>(), CombatSide.Enemy);

        Creature third = await Task.Run(
                () => combatState.AddMonster(Clone<NarrowRangeMonster>(), CombatSide.Enemy))
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotEqual(first.MaxHp, second.MaxHp);
        Assert.InRange(third.MaxHp, 10, 11);
        Assert.Contains(third.MaxHp, new[] { first.MaxHp, second.MaxHp });
    }

    [Fact]
    public void AddMonster_ExhaustedFallbackConsumesRngAndCanRollEitherEndpoint()
    {
        int[] fallbackRolls = Enumerable.Range(0, 16).Select(index =>
        {
            var state = new CombatState(new FakeRunState($"monster-hp-fallback-{index}"));
            state.AddMonster(Clone<NarrowRangeMonster>(), CombatSide.Enemy);
            state.AddMonster(Clone<NarrowRangeMonster>(), CombatSide.Enemy);
            int counterBefore = state.RunState.Rng.Niche.Counter;
            Creature fallback = state.AddMonster(Clone<NarrowRangeMonster>(), CombatSide.Enemy);
            Assert.Equal(counterBefore + 1, state.RunState.Rng.Niche.Counter);
            return fallback.MaxHp;
        }).ToArray();

        Assert.Contains(10, fallbackRolls);
        Assert.Contains(11, fallbackRolls);
    }

    [Fact]
    public void AddMonster_HpSequenceIsDeterministicForSameSeedAndVariesAcrossSeeds()
    {
        int[] first = RollSequence("monster-hp-deterministic");
        int[] second = RollSequence("monster-hp-deterministic");

        Assert.Equal(first, second);

        int[] firstRolls = Enumerable.Range(0, 32)
            .Select(index =>
            {
                var state = new CombatState(new FakeRunState($"monster-hp-seed-{index}"));
                return state.AddMonster(Clone<WideRangeMonster>(), CombatSide.Enemy).MaxHp;
            })
            .ToArray();
        Assert.Contains(10, firstRolls);
        Assert.Contains(14, firstRolls);
        Assert.True(firstRolls.Distinct().Count() > 1);
    }

    [Fact]
    public void AddMonster_PlayerAndMonsterOnOtherSide_DoNotReserveEnemyHp()
    {
        var runState = new FakeRunState("monster-hp-side-isolation");
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.Creature.SetMaxHpInternal(11m);
        runState.AddPlayer(player);
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        Creature alliedMonster = combatState.AddMonster(Clone<FixedElevenHpMonster>(), CombatSide.Player);

        Creature enemy = combatState.AddMonster(Clone<NarrowRangeMonster>(), CombatSide.Enemy);

        Assert.Equal(11, player.Creature.MaxHp);
        Assert.Equal(11, alliedMonster.MaxHp);
        Assert.Equal(11, enemy.MaxHp);
    }

    [Fact]
    public void AddMonster_InvalidHpRangeDoesNotPartiallyRegisterOrConsumeCombatId()
    {
        var combatState = new CombatState(new FakeRunState("monster-hp-invalid-range"));
        InvalidRangeMonster invalid = Clone<InvalidRangeMonster>();

        Assert.Throws<InvalidOperationException>(() =>
            combatState.AddMonster(invalid, CombatSide.Enemy));

        Assert.Empty(combatState.Enemies);
        Assert.Null(invalid.Creature);
        Creature valid = combatState.AddMonster(Clone<FixedHpMonster>(), CombatSide.Enemy);
        Assert.Equal(0u, valid.CombatId);
    }

    [Fact]
    public async Task CombatRoom_InitialBatchRollsUniqueHpBeforeMonsterSetup()
    {
        var runState = new RunState("monster-hp-initial-batch", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        NarrowRangeMonster first = Clone<NarrowRangeMonster>();
        NarrowRangeMonster second = Clone<NarrowRangeMonster>();
        var room = new CombatRoom(() => new MonsterModel[] { first, second });
        runState.PushRoom(room);

        await room.Enter(runState);

        Creature[] enemies = room.Engine.State.Enemies.ToArray();
        Assert.Equal(2, enemies.Length);
        Assert.NotEqual(enemies[0].MaxHp, enemies[1].MaxHp);
        Assert.All(enemies, enemy =>
        {
            var monster = Assert.IsType<NarrowRangeMonster>(enemy.Monster);
            Assert.Equal(enemy.MaxHp, monster.ObservedHpDuringSetup);
        });
    }

    [Fact]
    public void SetUniqueMonsterHpValue_PlayerThrowsWithoutChangingHpOrRng()
    {
        var runState = new FakeRunState("monster-hp-player");
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        int hpBefore = player.Creature.CurrentHp;
        int maxHpBefore = player.Creature.MaxHp;
        int nicheCounterBefore = runState.Rng.Niche.Counter;

        Assert.Throws<InvalidOperationException>(() =>
            player.Creature.SetUniqueMonsterHpValue(new[] { player.Creature }, runState.Rng.Niche));

        Assert.Equal(hpBefore, player.Creature.CurrentHp);
        Assert.Equal(maxHpBefore, player.Creature.MaxHp);
        Assert.Equal(nicheCounterBefore, runState.Rng.Niche.Counter);
    }

    [Fact]
    public async Task CreatureCmdAdd_DynamicMonsterUsesSameHpDeduplicationRuleBeforeSetup()
    {
        var runState = new FakeRunState("monster-hp-dynamic");
        ICombatState combatState = new CombatState(runState);
        Creature existing = combatState.AddMonster(Clone<NarrowRangeMonster>(), CombatSide.Enemy, "existing");
        uint? existingCombatId = existing.CombatId;
        int existingHp = existing.MaxHp;

        Creature added = await CreatureCmd.Add(
            Clone<NarrowRangeMonster>(),
            combatState,
            CombatSide.Enemy,
            "dynamic");

        Assert.NotEqual(existingHp, added.MaxHp);
        Assert.Equal(added.MaxHp, added.CurrentHp);
        Assert.Equal(existingCombatId, existing.CombatId);
        Assert.Equal("existing", existing.SlotName);
        Assert.Equal(1u, added.CombatId);
        Assert.Equal("dynamic", added.SlotName);
        Assert.True(((NarrowRangeMonster)added.Monster!).ObservedHpDuringSetup.HasValue);
        Assert.Equal(added.MaxHp, ((NarrowRangeMonster)added.Monster!).ObservedHpDuringSetup);
    }

    [Fact]
    public void AddMonster_FixedHpRangeConsumesNicheRngEvenWhenValueIsAlreadyUsed()
    {
        var runState = new FakeRunState("monster-hp-fixed");
        var combatState = new CombatState(runState);
        int counterBefore = runState.Rng.Niche.Counter;

        var nativeProbe = runState.Rng.Niche.CloneExact();
        Assert.Equal(10, nativeProbe.NextItem(new[] { 10 }));
        Creature first = combatState.AddMonster(Clone<FixedHpMonster>(), CombatSide.Enemy);
        Assert.Equal(counterBefore + 1, runState.Rng.Niche.Counter);
        Assert.Equal(nativeProbe.ToSerializable(), runState.Rng.Niche.ToSerializable());
        Assert.Equal(10, nativeProbe.NextInt(10, 11));
        Creature second = combatState.AddMonster(Clone<FixedHpMonster>(), CombatSide.Enemy);

        Assert.Equal(10, first.MaxHp);
        Assert.Equal(10, first.CurrentHp);
        Assert.Equal(10, second.MaxHp);
        Assert.Equal(10, second.CurrentHp);
        Assert.Equal(counterBefore + 2, runState.Rng.Niche.Counter);
        Assert.Equal(nativeProbe.ToSerializable(), runState.Rng.Niche.ToSerializable());
    }

    [Fact]
    public void AddMonster_RangeWiderThanIntMaxUsesSafeWidthWithoutPartialRegistration()
    {
        var runState = new FakeRunState("monster-hp-wide-int-range");
        var combatState = new CombatState(runState);
        int counterBefore = runState.Rng.Niche.Counter;

        Creature creature = combatState.AddMonster(Clone<NonNegativeIntRangeMonster>(), CombatSide.Enemy);

        Assert.InRange(creature.MaxHp, 0, int.MaxValue);
        Assert.Single(combatState.Enemies);
        Assert.Same(creature, combatState.Enemies[0]);
        Assert.Equal(0u, creature.CombatId);
        Assert.Equal(counterBefore + 1, runState.Rng.Niche.Counter);
    }

    [Fact]
    public void AddMonster_IntMaxEndpointAndExhaustedFallbackAreOverflowSafe()
    {
        var runState = new FakeRunState("monster-hp-int-max-endpoint");
        var combatState = new CombatState(runState);

        Creature first = combatState.AddMonster(Clone<IntMaxRangeMonster>(), CombatSide.Enemy);
        Creature second = combatState.AddMonster(Clone<IntMaxRangeMonster>(), CombatSide.Enemy);
        int counterBeforeFallback = runState.Rng.Niche.Counter;
        Creature fallback = combatState.AddMonster(Clone<IntMaxRangeMonster>(), CombatSide.Enemy);

        Assert.InRange(first.MaxHp, int.MaxValue - 1, int.MaxValue);
        Assert.InRange(second.MaxHp, int.MaxValue - 1, int.MaxValue);
        Assert.NotEqual(first.MaxHp, second.MaxHp);
        Assert.Contains(int.MaxValue, new[] { first.MaxHp, second.MaxHp });
        Assert.InRange(fallback.MaxHp, int.MaxValue - 1, int.MaxValue);
        Assert.Equal(counterBeforeFallback + 1, runState.Rng.Niche.Counter);
    }

    [Fact]
    public void AddMonster_FullIntRangeUsesSafeOffsetWithoutMaterializingCandidates()
    {
        var runState = new FakeRunState("monster-hp-full-int-range");
        var combatState = new CombatState(runState);

        Creature creature = combatState.AddMonster(Clone<FullIntRangeMonster>(), CombatSide.Enemy);

        Assert.InRange(creature.MaxHp, int.MinValue, int.MaxValue);
        Assert.Equal(1, runState.Rng.Niche.Counter);
    }

    private static T Clone<T>() where T : MonsterModel =>
        (T)ModelDb.Monster<T>().MutableClone();

    private static int[] RollSequence(string seed)
    {
        var combatState = new CombatState(new FakeRunState(seed));
        return Enumerable.Range(0, 4)
            .Select(_ => combatState.AddMonster(Clone<WideRangeMonster>(), CombatSide.Enemy).MaxHp)
            .ToArray();
    }

    private static MonsterMoveStateMachine CreateMoveStateMachine()
    {
        var move = new MoveState("WAIT", _ => Task.CompletedTask, new BuffIntent());
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }

    private sealed class FakeRunState(string seed) : IRunState
    {
        private readonly List<Player> _players = new();

        public RunRngSet Rng { get; } = new(seed);

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => _players;
        public int TotalFloor => 0;

        public AbstractRoom? CurrentRoom => null;

        public void AddPlayer(Player player) => _players.Add(player);

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? Array.Empty<AbstractModel>();
    }

    private sealed class WideRangeMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 14;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => CreateMoveStateMachine();
    }

    private sealed class OtherWideRangeMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 14;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => CreateMoveStateMachine();
    }

    private sealed class NarrowRangeMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 11;

        public int? ObservedHpDuringSetup { get; private set; }

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            ObservedHpDuringSetup = Creature.MaxHp;
            return CreateMoveStateMachine();
        }

        protected override void AfterCloned()
        {
            base.AfterCloned();
            ObservedHpDuringSetup = null;
        }
    }

    private sealed class FixedHpMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 10;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => CreateMoveStateMachine();
    }

    private sealed class FixedElevenHpMonster : MonsterModel
    {
        public override int MinInitialHp => 11;

        public override int MaxInitialHp => 11;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => CreateMoveStateMachine();
    }

    private sealed class NonNegativeIntRangeMonster : MonsterModel
    {
        public override int MinInitialHp => 0;

        public override int MaxInitialHp => int.MaxValue;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => CreateMoveStateMachine();
    }

    private sealed class IntMaxRangeMonster : MonsterModel
    {
        public override int MinInitialHp => int.MaxValue - 1;

        public override int MaxInitialHp => int.MaxValue;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => CreateMoveStateMachine();
    }

    private sealed class FullIntRangeMonster : MonsterModel
    {
        public override int MinInitialHp => int.MinValue;

        public override int MaxInitialHp => int.MaxValue;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => CreateMoveStateMachine();
    }

    private sealed class InvalidRangeMonster : MonsterModel
    {
        public override int MinInitialHp => 12;

        public override int MaxInitialHp => 11;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => CreateMoveStateMachine();
    }
}
