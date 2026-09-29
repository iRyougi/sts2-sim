namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class TwigSlimeMTests : IDisposable
{
    private readonly Task15MonsterTestFixture _fixture = new(typeof(TwigSlimeM));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 26, 28)]
    [InlineData((int)AscensionLevel.ToughEnemies, 27, 29)]
    public async Task HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (TwigSlimeM monster, _, _) = await Task15MonsterTestFixture.CreateCombatAsync<TwigSlimeM>(
            ascension,
            $"twig-slime-m-hp-{ascension}",
            playerCount: 1);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 12)]
    public async Task StickyThenPounce_UsesAuthoritativeEffectsForEveryPlayer(
        int ascension,
        int expectedDamage)
    {
        (TwigSlimeM monster, var room, IReadOnlyList<Player> players) =
            await Task15MonsterTestFixture.CreateCombatAsync<TwigSlimeM>(
                ascension,
                $"twig-slime-m-effects-{ascension}");
        IReadOnlyList<RingingPower> ringing = await Task15MonsterTestFixture.ApplyRingingAsync(room, players);
        int[] generatedBefore = players
            .Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat)
            .ToArray();

        Assert.Equal("STICKY_SHOT_MOVE", monster.NextMove!.StateId);
        StatusIntent stickyIntent = Assert.IsType<StatusIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(1, stickyIntent.Count);

        await Task15MonsterTestFixture.PerformAndRoll(monster, room);

        Assert.Equal("POKEY_POUNCE_MOVE", monster.NextMove!.StateId);
        for (int index = 0; index < players.Count; index++)
        {
            Player player = players[index];
            Slimed generated = Assert.Single(player.PlayerCombatState!.DiscardPile.Cards.OfType<Slimed>());
            Assert.Equal(generatedBefore[index] + 1, player.PlayerCombatState.CardsGeneratedThisCombat);
            Assert.Same(player, generated.Owner);
            Assert.Same(player.PlayerCombatState.DiscardPile, generated.Pile);
            Assert.Same(room.Engine.State, generated.CombatState);
            Assert.True(ringing[index].IsRinging(generated));
        }

        SingleAttackIntent pounceIntent = Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedDamage, pounceIntent.GetSingleDamage(room.Engine.State.Allies, monster.Creature));
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();

        await Task15MonsterTestFixture.PerformAndRoll(monster, room);

        Assert.Contains(monster.NextMove!.StateId, new[] { "POKEY_POUNCE_MOVE", "STICKY_SHOT_MOVE" });
        Assert.Equal(hpBefore.Select(hp => hp - expectedDamage), players.Select(player => player.Creature.CurrentHp));
        Assert.Equal(
            players.Select((player, index) => generatedBefore[index] + 1),
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));
    }

    [Fact]
    public async Task RandomBranch_HasExactStructureAndRuntimeWeightAndRepeatSemantics()
    {
        int secondPounceCount = 0;
        var seen = new HashSet<string>();
        bool sawConsecutivePounces = false;

        for (int seed = 0; seed < 256; seed++)
        {
            (TwigSlimeM monster, var room, _) = await Task15MonsterTestFixture.CreateCombatAsync<TwigSlimeM>(
                0,
                $"twig-slime-m-random-{seed}",
                playerCount: 1);
            var sequence = new List<string> { monster.NextMove!.StateId };

            for (int turn = 0; turn < 12; turn++)
            {
                monster.MoveStateMachine!.OnMovePerformed(monster.NextMove!);
                monster.RollMove(room.Engine.State.Allies);
                sequence.Add(monster.NextMove!.StateId);
            }

            Assert.Equal("STICKY_SHOT_MOVE", sequence[0]);
            Assert.Equal("POKEY_POUNCE_MOVE", sequence[1]);
            if (sequence[2] == "POKEY_POUNCE_MOVE")
            {
                secondPounceCount++;
            }

            for (int index = 1; index < sequence.Count; index++)
            {
                Assert.Contains(sequence[index], new[] { "POKEY_POUNCE_MOVE", "STICKY_SHOT_MOVE" });
                Assert.False(
                    sequence[index - 1] == "STICKY_SHOT_MOVE" && sequence[index] == "STICKY_SHOT_MOVE",
                    string.Join(",", sequence));
                if (index >= 2)
                    Assert.False(sequence.Skip(index - 2).Take(3).All(id => id == "POKEY_POUNCE_MOVE"),
                        string.Join(",", sequence));
                sawConsecutivePounces |=
                    sequence[index - 1] == "POKEY_POUNCE_MOVE" && sequence[index] == "POKEY_POUNCE_MOVE";
            }

            seen.UnionWith(sequence);
        }

        Assert.InRange(secondPounceCount, 100, 156);
        Assert.True(sawConsecutivePounces);
        Assert.Equal(
            new[] { "POKEY_POUNCE_MOVE", "STICKY_SHOT_MOVE" },
            seen.OrderBy(id => id));

        (TwigSlimeM inspected, _, _) = await Task15MonsterTestFixture.CreateCombatAsync<TwigSlimeM>(
            0,
            "twig-slime-m-structure",
            playerCount: 1);
        RandomBranchState branch = Assert.IsType<RandomBranchState>(inspected.MoveStateMachine!.States["RAND"]);
        Assert.Collection(
            branch.States,
            state => AssertBranch(state, "POKEY_POUNCE_MOVE", MoveRepeatType.CanRepeatXTimes, 1f),
            state => AssertBranch(state, "STICKY_SHOT_MOVE", MoveRepeatType.CannotRepeat, 1f));
        Assert.Equal(2, branch.States[0].MaxTimes);
    }

    private static void AssertBranch(
        RandomBranchState.StateWeight state,
        string expectedStateId,
        MoveRepeatType expectedRepeatType,
        float expectedWeight)
    {
        Assert.Equal(expectedStateId, state.StateId);
        Assert.Equal(expectedRepeatType, state.RepeatType);
        Assert.Equal(0, state.Cooldown);
        Assert.Equal(expectedWeight, state.GetWeight());
    }
}
