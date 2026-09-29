namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class SlitheringStranglerTests : IDisposable
{
    private readonly Task18MonsterTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 53, 55)]
    [InlineData((int)AscensionLevel.ToughEnemies, 54, 56)]
    public async Task HpRangeAndOpening_UseAuthoritativeValues(
        int ascension,
        int expectedMinHp,
        int expectedMaxHp)
    {
        (SlitheringStrangler monster, _, _) =
            await Task18MonsterTestFixture.CreateCombatAsync<SlitheringStrangler>(
                ascension,
                $"strangler-hp-{ascension}");

        Assert.Equal(expectedMinHp, monster.MinInitialHp);
        Assert.Equal(expectedMaxHp, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMinHp, expectedMaxHp);
        Assert.Equal("CONSTRICT", monster.NextMove!.StateId);
    }

    [Theory]
    [InlineData(0, 7, 12)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 8, 13)]
    public async Task StateGraph_IntentsWeightsAndRepeatTypesMatchAuthoritativeSource(
        int ascension,
        int expectedThwackDamage,
        int expectedLashDamage)
    {
        (SlitheringStrangler monster, _, _) =
            await Task18MonsterTestFixture.CreateCombatAsync<SlitheringStrangler>(
                ascension,
                $"strangler-structure-{ascension}");
        MonsterMoveStateMachine machine = monster.MoveStateMachine!;
        MoveState constrict = Assert.IsType<MoveState>(machine.States["CONSTRICT"]);
        MoveState thwack = Assert.IsType<MoveState>(machine.States["THWACK"]);
        MoveState lash = Assert.IsType<MoveState>(machine.States["LASH"]);
        RandomBranchState random = Assert.IsType<RandomBranchState>(machine.States["rand"]);

        Assert.Equal(4, machine.States.Count);
        Assert.IsType<DebuffIntent>(Assert.Single(constrict.Intents));
        Assert.Collection(
            thwack.Intents,
            intent => Assert.Equal(expectedThwackDamage,
                Assert.IsType<SingleAttackIntent>(intent)
                    .GetSingleDamage(PlayerTarget, monster.Creature)),
            intent => Assert.IsType<DefendIntent>(intent));
        Assert.Equal(expectedLashDamage,
            Assert.IsType<SingleAttackIntent>(Assert.Single(lash.Intents))
                .GetSingleDamage(PlayerTarget, monster.Creature));
        Assert.Same(random, constrict.FollowUpState);
        Assert.Same(constrict, thwack.FollowUpState);
        Assert.Same(constrict, lash.FollowUpState);
        Assert.Collection(
            random.States,
            state => AssertBranch(state, "THWACK"),
            state => AssertBranch(state, "LASH"));
    }

    [Fact]
    public async Task SeededRandomBranch_ObservesBothExactOutcomesWithoutDistributionBounds()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 64; seed++)
        {
            (SlitheringStrangler monster, _, _) =
                await Task18MonsterTestFixture.CreateCombatAsync<SlitheringStrangler>(
                    0,
                    $"strangler-seeded-branch-{seed}");
            Assert.Equal("CONSTRICT", monster.NextMove!.StateId);

            Task18MonsterTestFixture.RollAfterPerformed(monster);

            Assert.Contains(monster.NextMove!.StateId, new[] { "THWACK", "LASH" });
            seen.Add(monster.NextMove.StateId);
            Task18MonsterTestFixture.RollAfterPerformed(monster);
            Assert.Equal("CONSTRICT", monster.NextMove!.StateId);
        }

        Assert.Equal(new[] { "LASH", "THWACK" }, seen.OrderBy(value => value));
    }

    [Theory]
    [InlineData(0, 7, 12)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 8, 13)]
    public async Task Moves_UseCombatCommandsAgainstEveryPlayerAndConstrictLifecycle(
        int ascension,
        int expectedThwackLoss,
        int expectedLashLoss)
    {
        (SlitheringStrangler monster, var room,
            IReadOnlyList<Sts2Sim.Core.Entities.Players.Player> players) =
            await Task18MonsterTestFixture.CreateCombatAsync<SlitheringStrangler>(
                ascension,
                $"strangler-constrict-{ascension}",
                playerCount: 2);
        Task18MonsterTestFixture.ForceMove(monster, "CONSTRICT");

        await monster.PerformMove();

        Assert.All(players, player =>
        {
            ConstrictPower constrict = Assert.IsType<ConstrictPower>(
                player.Creature.GetPower<ConstrictPower>());
            Assert.Equal(3, constrict.Amount);
            Assert.Same(monster.Creature, constrict.Applier);
        });
        int[] hpBeforeConstrict = players.Select(player => player.Creature.CurrentHp).ToArray();

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            players.Select(player => player.Creature).ToArray());

        Assert.Equal(hpBeforeConstrict.Select(hp => hp - 3),
            players.Select(player => player.Creature.CurrentHp));

        (int[] thwackLosses, SlitheringStrangler thwack) =
            await PerformMoveAndMeasureAllPlayerLosses(ascension, "THWACK");
        Assert.All(thwackLosses, loss => Assert.Equal(expectedThwackLoss, loss));
        Assert.Equal(5, thwack.Creature.Block);

        (int[] lashLosses, SlitheringStrangler lash) =
            await PerformMoveAndMeasureAllPlayerLosses(ascension, "LASH");
        Assert.All(lashLosses, loss => Assert.Equal(expectedLashLoss, loss));
        Assert.Equal(0, lash.Creature.Block);
    }

    private static IReadOnlyList<Creature> PlayerTarget { get; } =
        new[] { Creature.CreateStandaloneForTests(100, 100) };

    private static void AssertBranch(
        RandomBranchState.StateWeight state,
        string expectedStateId)
    {
        Assert.Equal(expectedStateId, state.StateId);
        Assert.Equal(MoveRepeatType.CanRepeatForever, state.RepeatType);
        Assert.Equal(0, state.Cooldown);
        Assert.Equal(0, state.MaxTimes);
        Assert.Equal(1f, state.GetWeight());
    }

    private static async Task<(int[] Losses, SlitheringStrangler Monster)>
        PerformMoveAndMeasureAllPlayerLosses(int ascension, string stateId)
    {
        (SlitheringStrangler monster, _,
            IReadOnlyList<Sts2Sim.Core.Entities.Players.Player> players) =
            await Task18MonsterTestFixture.CreateCombatAsync<SlitheringStrangler>(
                ascension,
                $"strangler-effect-{stateId}-{ascension}",
                playerCount: 2);
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();
        Task18MonsterTestFixture.ForceMove(monster, stateId);

        await monster.PerformMove();

        int[] losses = players.Select((player, index) =>
            hpBefore[index] - player.Creature.CurrentHp).ToArray();
        return (losses, monster);
    }
}
