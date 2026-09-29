namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class InkletTests : IDisposable
{
    private readonly Task17MonsterTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void MutableClones_IsolateMiddleInkletConfiguration()
    {
        var first = (Inklet)ModelDb.Monster<Inklet>().MutableClone();
        var middle = (Inklet)ModelDb.Monster<Inklet>().MutableClone();
        var third = (Inklet)ModelDb.Monster<Inklet>().MutableClone();

        middle.MiddleInklet = true;

        Assert.False(first.MiddleInklet);
        Assert.True(middle.MiddleInklet);
        Assert.False(third.MiddleInklet);
        Assert.NotSame(first, middle);
        Assert.NotSame(middle, third);
    }

    [Theory]
    [InlineData(0, 11, 17)]
    [InlineData((int)AscensionLevel.ToughEnemies, 12, 18)]
    public async Task ThreeBodyConfiguration_HasDistinctInstancesHpSlipperyAndMiddleOpening(
        int ascension,
        int expectedMinHp,
        int expectedMaxHp)
    {
        (IReadOnlyList<Inklet> inklets, _, _) =
            await Task17MonsterTestFixture.CreateCombatAsync<Inklet>(
                ascension,
                $"inklets-three-body-{ascension}",
                (null, null),
                (inklet => inklet.MiddleInklet = true, null),
                (null, null));

        Assert.Equal(3, inklets.Count);
        Assert.Equal(3, inklets.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(new[] { false, true, false }, inklets.Select(inklet => inklet.MiddleInklet));
        Assert.Equal(new[] { "JAB_MOVE", "WHIRLWIND_MOVE", "JAB_MOVE" },
            inklets.Select(inklet => inklet.NextMove!.StateId));
        Assert.All(inklets, inklet =>
        {
            Assert.Equal(expectedMinHp, inklet.MinInitialHp);
            Assert.Equal(expectedMaxHp, inklet.MaxInitialHp);
            Assert.InRange(inklet.Creature.MaxHp, expectedMinHp, expectedMaxHp);
            Assert.Equal(inklet.Creature.MaxHp, inklet.Creature.CurrentHp);
            Assert.Equal(1, Assert.IsType<SlipperyPower>(
                inklet.Creature.GetPower<SlipperyPower>()).Amount);
        });
    }

    [Theory]
    [InlineData(0, 3, 2, 10)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 4, 3, 11)]
    public void StateGraph_HasExactTopologyIntentsAndRepeatMetadata(
        int ascension,
        int expectedJabDamage,
        int expectedWhirlwindDamage,
        int expectedPiercingDamage)
    {
        Inklet inklet = Task17MonsterTestFixture.CreateStructuralMonster<Inklet>(
            ascension,
            $"inklet-structure-{ascension}");
        MonsterMoveStateMachine machine = inklet.MoveStateMachine!;
        MoveState jab = Assert.IsType<MoveState>(machine.States["JAB_MOVE"]);
        MoveState whirlwind = Assert.IsType<MoveState>(machine.States["WHIRLWIND_MOVE"]);
        MoveState piercing = Assert.IsType<MoveState>(machine.States["PIERCING_GAZE_MOVE"]);
        RandomBranchState random = Assert.IsType<RandomBranchState>(machine.States["RAND"]);

        Assert.Equal(4, machine.States.Count);
        Assert.DoesNotContain("INIT_RAND", machine.States.Keys);
        Assert.Equal(expectedJabDamage, SingleDamage(jab, inklet));
        MultiAttackIntent whirlwindIntent = Assert.IsType<MultiAttackIntent>(
            Assert.Single(whirlwind.Intents));
        Assert.Equal(expectedWhirlwindDamage,
            whirlwindIntent.GetSingleDamage(PlayerTarget, inklet.Creature));
        Assert.Equal(3, whirlwindIntent.Repeats);
        Assert.Equal(expectedPiercingDamage, SingleDamage(piercing, inklet));
        Assert.Same(random, jab.FollowUpState);
        Assert.Same(jab, whirlwind.FollowUpState);
        Assert.Same(jab, piercing.FollowUpState);
        Assert.Collection(
            random.States,
            state => AssertBranch(state, "PIERCING_GAZE_MOVE"),
            state => AssertBranch(state, "WHIRLWIND_MOVE"));
    }

    [Theory]
    [InlineData(0, 3, 6, 10)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 4, 9, 11)]
    public async Task Moves_DealAuthoritativeDamageThroughCombatCommands(
        int ascension,
        int expectedJabLoss,
        int expectedWhirlwindLoss,
        int expectedPiercingLoss)
    {
        Assert.Equal(expectedJabLoss,
            await PerformMoveAndMeasureHpLoss(ascension, "JAB_MOVE"));
        Assert.Equal(expectedWhirlwindLoss,
            await PerformMoveAndMeasureHpLoss(ascension, "WHIRLWIND_MOVE"));
        Assert.Equal(expectedPiercingLoss,
            await PerformMoveAndMeasureHpLoss(ascension, "PIERCING_GAZE_MOVE"));
    }

    [Fact]
    public void RandomSpecialBranch_ProducesBothOutcomesAndAlwaysReturnsToJab()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 128; seed++)
        {
            Inklet inklet = Task17MonsterTestFixture.CreateStructuralMonster<Inklet>(
                0,
                $"inklet-random-{seed}");
            Assert.Equal("JAB_MOVE", inklet.NextMove!.StateId);

            inklet.MoveStateMachine!.OnMovePerformed(inklet.NextMove);
            inklet.RollMove(inklet.Creature.CombatState!.Allies);
            seen.Add(inklet.NextMove!.StateId);
            Assert.Contains(inklet.NextMove.StateId,
                new[] { "PIERCING_GAZE_MOVE", "WHIRLWIND_MOVE" });

            inklet.MoveStateMachine.OnMovePerformed(inklet.NextMove);
            inklet.RollMove(inklet.Creature.CombatState.Allies);
            Assert.Equal("JAB_MOVE", inklet.NextMove!.StateId);
        }

        Assert.Equal(
            new[] { "PIERCING_GAZE_MOVE", "WHIRLWIND_MOVE" },
            seen.OrderBy(value => value));
    }

    private static IReadOnlyList<Creature> PlayerTarget { get; } =
        new[] { Creature.CreateStandaloneForTests(100, 100) };

    private static int SingleDamage(MoveState move, Inklet inklet) =>
        Assert.IsType<SingleAttackIntent>(Assert.Single(move.Intents))
            .GetSingleDamage(PlayerTarget, inklet.Creature);

    private static void AssertBranch(RandomBranchState.StateWeight state, string expectedStateId)
    {
        Assert.Equal(expectedStateId, state.StateId);
        Assert.Equal(MoveRepeatType.CannotRepeat, state.RepeatType);
        Assert.Equal(0, state.Cooldown);
        Assert.Equal(0, state.MaxTimes);
        Assert.Equal(1f, state.GetWeight());
    }

    private static async Task<int> PerformMoveAndMeasureHpLoss(int ascension, string stateId)
    {
        (IReadOnlyList<Inklet> inklets, _, var player) =
            await Task17MonsterTestFixture.CreateCombatAsync<Inklet>(
                ascension,
                $"inklet-effect-{stateId}-{ascension}",
                (null, null));
        Inklet inklet = Assert.Single(inklets);
        Task17MonsterTestFixture.ForceMove(inklet, stateId);
        int hpBefore = player.Creature.CurrentHp;

        await inklet.PerformMove();

        return hpBefore - player.Creature.CurrentHp;
    }
}
