namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class VineShamblerTests : IDisposable
{
    private readonly Task18MonsterTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 61)]
    [InlineData((int)AscensionLevel.ToughEnemies, 64)]
    public async Task FixedHpAndOpening_UseAuthoritativeValues(int ascension, int expectedHp)
    {
        (VineShambler monster, _, _) =
            await Task18MonsterTestFixture.CreateCombatAsync<VineShambler>(
                ascension,
                $"vine-shambler-hp-{ascension}");

        Assert.Equal(expectedHp, monster.MinInitialHp);
        Assert.Equal(expectedHp, monster.MaxInitialHp);
        Assert.Equal(expectedHp, monster.Creature.MaxHp);
        Assert.Equal("SWIPE_MOVE", monster.NextMove!.StateId);
    }

    [Theory]
    [InlineData(0, 8, 6, 16)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 9, 7, 18)]
    public async Task StateGraph_IntentsAndCycleMatchAuthoritativeSource(
        int ascension,
        int expectedGraspingDamage,
        int expectedSwipeDamage,
        int expectedChompDamage)
    {
        (VineShambler monster, _, _) =
            await Task18MonsterTestFixture.CreateCombatAsync<VineShambler>(
                ascension,
                $"vine-shambler-structure-{ascension}");
        MonsterMoveStateMachine machine = monster.MoveStateMachine!;
        MoveState grasping = Assert.IsType<MoveState>(machine.States["GRASPING_VINES_MOVE"]);
        MoveState swipe = Assert.IsType<MoveState>(machine.States["SWIPE_MOVE"]);
        MoveState chomp = Assert.IsType<MoveState>(machine.States["CHOMP_MOVE"]);

        Assert.Equal(3, machine.States.Count);
        Assert.Collection(
            grasping.Intents,
            intent => Assert.Equal(expectedGraspingDamage,
                Assert.IsType<SingleAttackIntent>(intent)
                    .GetSingleDamage(PlayerTarget, monster.Creature)),
            intent => Assert.Equal(IntentType.CardDebuff,
                Assert.IsType<CardDebuffIntent>(intent).IntentType));
        MultiAttackIntent swipeIntent = Assert.IsType<MultiAttackIntent>(
            Assert.Single(swipe.Intents));
        Assert.Equal(expectedSwipeDamage,
            swipeIntent.GetSingleDamage(PlayerTarget, monster.Creature));
        Assert.Equal(2, swipeIntent.Repeats);
        Assert.Equal(expectedChompDamage,
            Assert.IsType<SingleAttackIntent>(Assert.Single(chomp.Intents))
                .GetSingleDamage(PlayerTarget, monster.Creature));
        Assert.Same(grasping, swipe.FollowUpState);
        Assert.Same(chomp, grasping.FollowUpState);
        Assert.Same(swipe, chomp.FollowUpState);

        Assert.Equal("SWIPE_MOVE", monster.NextMove!.StateId);
        Task18MonsterTestFixture.RollAfterPerformed(monster);
        Assert.Equal("GRASPING_VINES_MOVE", monster.NextMove!.StateId);
        Task18MonsterTestFixture.RollAfterPerformed(monster);
        Assert.Equal("CHOMP_MOVE", monster.NextMove!.StateId);
        Task18MonsterTestFixture.RollAfterPerformed(monster);
        Assert.Equal("SWIPE_MOVE", monster.NextMove!.StateId);
    }

    [Theory]
    [InlineData(0, 8, 12, 16)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 9, 14, 18)]
    public async Task Moves_UseCombatCommandsAgainstEveryPlayerAndTangledLifecycle(
        int ascension,
        int expectedGraspingLoss,
        int expectedSwipeLoss,
        int expectedChompLoss)
    {
        (VineShambler monster, var room, IReadOnlyList<Sts2Sim.Core.Entities.Players.Player> players) =
            await Task18MonsterTestFixture.CreateCombatAsync<VineShambler>(
                ascension,
                $"vine-shambler-grasping-{ascension}",
                playerCount: 2);
        var attacks = players.Select(player => player.PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards)
            .First(card => card.Type == CardType.Attack)).ToArray();
        int[] costsBefore = attacks.Select(card => card.EnergyCost).ToArray();
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();
        Task18MonsterTestFixture.ForceMove(monster, "GRASPING_VINES_MOVE");

        await monster.PerformMove();

        Assert.Equal(hpBefore.Select(hp => hp - expectedGraspingLoss),
            players.Select(player => player.Creature.CurrentHp));
        Assert.All(players, player =>
        {
            TangledPower tangled = Assert.IsType<TangledPower>(
                player.Creature.GetPower<TangledPower>());
            Assert.Equal(1, tangled.Amount);
            Assert.Same(monster.Creature, tangled.Applier);
        });
        Assert.Equal(costsBefore.Select(cost => cost + 1),
            attacks.Select(card => card.EnergyCost));

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            players.Select(player => player.Creature).ToArray());
        Assert.All(players, player => Assert.Null(player.Creature.GetPower<TangledPower>()));
        Assert.Equal(costsBefore, attacks.Select(card => card.EnergyCost));

        Assert.Equal(expectedSwipeLoss,
            await PerformMoveAndMeasureAllPlayerLosses(ascension, "SWIPE_MOVE"));
        Assert.Equal(expectedChompLoss,
            await PerformMoveAndMeasureAllPlayerLosses(ascension, "CHOMP_MOVE"));
    }

    private static IReadOnlyList<Creature> PlayerTarget { get; } =
        new[] { Creature.CreateStandaloneForTests(100, 100) };

    private static async Task<int> PerformMoveAndMeasureAllPlayerLosses(
        int ascension,
        string stateId)
    {
        (VineShambler monster, _, IReadOnlyList<Sts2Sim.Core.Entities.Players.Player> players) =
            await Task18MonsterTestFixture.CreateCombatAsync<VineShambler>(
                ascension,
                $"vine-shambler-effect-{stateId}-{ascension}",
                playerCount: 2);
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();
        Task18MonsterTestFixture.ForceMove(monster, stateId);

        await monster.PerformMove();

        int[] losses = players.Select((player, index) =>
            hpBefore[index] - player.Creature.CurrentHp).ToArray();
        Assert.Equal(2, losses.Length);
        Assert.Single(losses.Distinct());
        return losses[0];
    }
}
