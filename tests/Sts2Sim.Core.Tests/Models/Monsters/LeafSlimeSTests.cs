namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Models.Afflictions;

[Collection("ModelDb")]
public sealed class LeafSlimeSTests : IDisposable
{
    private readonly Task15MonsterTestFixture _fixture = new(typeof(LeafSlimeS));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 11, 15)]
    [InlineData((int)AscensionLevel.ToughEnemies, 12, 16)]
    public async Task HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (LeafSlimeS monster, _, _) = await Task15MonsterTestFixture.CreateCombatAsync<LeafSlimeS>(
            ascension,
            $"leaf-slime-s-hp-{ascension}",
            playerCount: 1);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 4)]
    public async Task GoopThenTackle_CompletesTheAuthoritativeLoopAndEffectsForEveryPlayer(
        int ascension,
        int expectedDamage)
    {
        (LeafSlimeS monster, CombatRoom room, IReadOnlyList<Player> players) =
            await CreateCombatStartingWithGoopAsync(ascension);
        IReadOnlyList<RingingPower> ringing = await Task15MonsterTestFixture.ApplyRingingAsync(room, players);
        int[] generatedBefore = players
            .Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat)
            .ToArray();

        Assert.Equal("GOOP_MOVE", monster.NextMove!.StateId);
        StatusIntent goopIntent = Assert.IsType<StatusIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(1, goopIntent.Count);

        await Task15MonsterTestFixture.PerformAndRoll(monster, room);

        Assert.Equal("TACKLE_MOVE", monster.NextMove!.StateId);
        for (int index = 0; index < players.Count; index++)
        {
            Player player = players[index];
            Slimed generated = Assert.Single(player.PlayerCombatState!.DiscardPile.Cards.OfType<Slimed>());
            Assert.Equal(generatedBefore[index], player.PlayerCombatState.CardsGeneratedThisCombat);
            Assert.Same(player, generated.Owner);
            Assert.Same(player.PlayerCombatState.DiscardPile, generated.Pile);
            Assert.Same(room.Engine.State, generated.CombatState);
            Assert.True(generated.Affliction is Ringing);
        }

        SingleAttackIntent tackleIntent = Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedDamage, tackleIntent.GetSingleDamage(room.Engine.State.Allies, monster.Creature));
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();

        await Task15MonsterTestFixture.PerformAndRoll(monster, room);

        Assert.Equal("GOOP_MOVE", monster.NextMove!.StateId);
        Assert.Equal(hpBefore.Select(hp => hp - expectedDamage), players.Select(player => player.Creature.CurrentHp));
        Assert.Equal(
            generatedBefore,
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));
    }

    [Fact]
    public async Task RandomBranch_FixedWitnessesPreserveBothOpenersAlternationAndRng()
    {
        var witnesses = new[]
        {
            (Seed: 0, FirstMove: "TACKLE_MOVE", InitialRng: (4065771415423447804UL, 7895922073370502719UL, 9144741505676130055UL, 7560711539809357584UL)),
            (Seed: 2, FirstMove: "GOOP_MOVE", InitialRng: (330409222913498179UL, 12030374533650227524UL, 2657979179174670448UL, 904773363353008229UL)),
        };
        foreach (var witness in witnesses)
        {
            (LeafSlimeS monster, var room, _) = await Task15MonsterTestFixture.CreateCombatAsync<LeafSlimeS>(
                0, $"leaf-slime-s-random-{witness.Seed}", playerCount: 1);
            MonsterMoveStateMachine machine = monster.MoveStateMachine!;
            RandomBranchState branch = Assert.IsType<RandomBranchState>(machine.States["RAND"]);
            Assert.Collection(
                branch.States,
                state => AssertBranch(state, "TACKLE_MOVE"),
                state => AssertBranch(state, "GOOP_MOVE"));
            var rng = room.Engine.State.RunState.Rng.MonsterAi;
            var initial = rng.ToSerializable();
            Assert.Equal(1, initial.counter);
            Assert.Equal(witness.InitialRng, (initial.state0, initial.state1, initial.state2, initial.state3));
            Assert.Equal(witness.FirstMove, monster.NextMove!.StateId);
            var expectedRng = rng.CloneExact();
            var expectedLog = new List<string> { witness.FirstMove };
            Assert.Equal(expectedLog, machine.StateLog.Select(state => state.Id));
            string otherMove = witness.FirstMove == "TACKLE_MOVE" ? "GOOP_MOVE" : "TACKLE_MOVE";
            for (int turn = 0; turn < 12; turn++)
            {
                // Even the sole eligible branch consumes one draw.
                expectedRng.NextFloat();
                machine.OnMovePerformed(monster.NextMove!);
                monster.RollMove(room.Engine.State.Allies);
                expectedLog.Add(turn % 2 == 0 ? otherMove : witness.FirstMove);
                Assert.True(monster.NextMove!.StateId == expectedLog[^1], $"seed={witness.Seed}, turn={turn}");
                Assert.Equal(expectedLog, machine.StateLog.Select(state => state.Id));
                Assert.Equal(expectedRng.ToSerializable(), rng.ToSerializable());
            }
        }
    }

    private static Task<(LeafSlimeS Monster, CombatRoom Room, IReadOnlyList<Player> Players)>
        CreateCombatStartingWithGoopAsync(int ascension) =>
        Task15MonsterTestFixture.CreateCombatAsync<LeafSlimeS>(
            ascension, $"leaf-slime-s-goop-{ascension}-0");

    private static void AssertBranch(RandomBranchState.StateWeight state, string expectedStateId)
    {
        Assert.Equal(expectedStateId, state.StateId);
        Assert.Equal(MoveRepeatType.CannotRepeat, state.RepeatType);
        Assert.Equal(0, state.Cooldown);
        Assert.Equal(1f, state.GetWeight());
    }
}
