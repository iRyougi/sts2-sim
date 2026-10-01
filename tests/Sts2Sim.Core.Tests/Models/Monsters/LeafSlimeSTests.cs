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
            Assert.Equal(generatedBefore[index] + 1, player.PlayerCombatState.CardsGeneratedThisCombat);
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
            players.Select((player, index) => generatedBefore[index] + 1),
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));
    }

    [Fact]
    public async Task RandomBranch_HasExactEqualWeightsAndStrictAlternationAcrossBoundedSeeds()
    {
        int tackleFirstCount = 0;
        var seen = new HashSet<string>();

        for (int seed = 0; seed < 256; seed++)
        {
            (LeafSlimeS monster, var room, _) = await Task15MonsterTestFixture.CreateCombatAsync<LeafSlimeS>(
                0,
                $"leaf-slime-s-random-{seed}",
                playerCount: 1);
            var sequence = new List<string> { monster.NextMove!.StateId };
            if (monster.NextMove.StateId == "TACKLE_MOVE")
            {
                tackleFirstCount++;
            }

            for (int turn = 0; turn < 12; turn++)
            {
                monster.MoveStateMachine!.OnMovePerformed(monster.NextMove!);
                monster.RollMove(room.Engine.State.Allies);
                sequence.Add(monster.NextMove!.StateId);
            }

            for (int index = 0; index < sequence.Count; index++)
            {
                Assert.Contains(sequence[index], new[] { "TACKLE_MOVE", "GOOP_MOVE" });
                if (index > 0)
                {
                    Assert.NotEqual(sequence[index - 1], sequence[index]);
                }
            }

            seen.UnionWith(sequence);
        }

        Assert.InRange(tackleFirstCount, 105, 151);
        Assert.Equal(new[] { "GOOP_MOVE", "TACKLE_MOVE" }, seen.OrderBy(id => id));

        (LeafSlimeS inspected, _, _) = await Task15MonsterTestFixture.CreateCombatAsync<LeafSlimeS>(
            0,
            "leaf-slime-s-structure",
            playerCount: 1);
        RandomBranchState branch = Assert.IsType<RandomBranchState>(inspected.MoveStateMachine!.States["RAND"]);
        Assert.Collection(
            branch.States,
            state => AssertBranch(state, "TACKLE_MOVE"),
            state => AssertBranch(state, "GOOP_MOVE"));
    }

    private static async Task<(LeafSlimeS Monster, CombatRoom Room, IReadOnlyList<Player> Players)>
        CreateCombatStartingWithGoopAsync(int ascension)
    {
        for (int seed = 0; seed < 64; seed++)
        {
            (LeafSlimeS monster, CombatRoom room, IReadOnlyList<Player> players) =
                await Task15MonsterTestFixture.CreateCombatAsync<LeafSlimeS>(
                    ascension,
                    $"leaf-slime-s-goop-{ascension}-{seed}");
            if (monster.NextMove!.StateId == "GOOP_MOVE")
            {
                return (monster, room, players);
            }
        }

        throw new Xunit.Sdk.XunitException("No GOOP_MOVE initial branch found in the bounded seed search.");
    }

    private static void AssertBranch(RandomBranchState.StateWeight state, string expectedStateId)
    {
        Assert.Equal(expectedStateId, state.StateId);
        Assert.Equal(MoveRepeatType.CannotRepeat, state.RepeatType);
        Assert.Equal(0, state.Cooldown);
        Assert.Equal(1f, state.GetWeight());
    }
}
