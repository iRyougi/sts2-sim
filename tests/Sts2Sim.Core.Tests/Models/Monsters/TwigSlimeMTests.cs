namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Models.Afflictions;

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
            Assert.Equal(generatedBefore[index], player.PlayerCombatState.CardsGeneratedThisCombat);
            Assert.Same(player, generated.Owner);
            Assert.Same(player.PlayerCombatState.DiscardPile, generated.Pile);
            Assert.Same(room.Engine.State, generated.CombatState);
            Assert.True(generated.Affliction is Ringing);
        }

        SingleAttackIntent pounceIntent = Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedDamage, pounceIntent.GetSingleDamage(room.Engine.State.Allies, monster.Creature));
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();

        await Task15MonsterTestFixture.PerformAndRoll(monster, room);

        Assert.Contains(monster.NextMove!.StateId, new[] { "POKEY_POUNCE_MOVE", "STICKY_SHOT_MOVE" });
        Assert.Equal(hpBefore.Select(hp => hp - expectedDamage), players.Select(player => player.Creature.CurrentHp));
        Assert.Equal(
            generatedBefore,
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));
    }

    [Fact]
    public async Task RandomBranch_FixedWitnessesPreserveMaxRepeatsAndRng()
    {
        const string sticky = "STICKY_SHOT_MOVE", pounce = "POKEY_POUNCE_MOVE";
        var witnesses = new[]
        {
            (Seed: 0, Moves: new[] { sticky, pounce, sticky, pounce, pounce, sticky, pounce, sticky, pounce, sticky, pounce, sticky, pounce },
                InitialRng: (9937034638475005148UL, 14376960492370911095UL, 10614205917749785383UL, 9053439162056752296UL)),
            (Seed: 2, Moves: new[] { sticky, pounce, pounce, sticky, pounce, pounce, sticky, pounce, pounce, sticky, pounce, sticky, pounce },
                InitialRng: (17159920646715611841UL, 13935912709447806983UL, 14200218110477237111UL, 8194906899183382082UL)),
        };
        foreach (var witness in witnesses)
        {
            (TwigSlimeM monster, var room, _) = await Task15MonsterTestFixture.CreateCombatAsync<TwigSlimeM>(
                0, $"twig-slime-m-random-{witness.Seed}", playerCount: 1);
            MonsterMoveStateMachine machine = monster.MoveStateMachine!;
            RandomBranchState branch = Assert.IsType<RandomBranchState>(machine.States["RAND"]);
            Assert.Collection(
                branch.States,
                state => AssertBranch(state, pounce, MoveRepeatType.CanRepeatXTimes, 1f),
                state => AssertBranch(state, sticky, MoveRepeatType.CannotRepeat, 1f));
            Assert.Equal(2, branch.States[0].MaxTimes);
            var rng = room.Engine.State.RunState.Rng.MonsterAi;
            var initial = rng.ToSerializable();
            Assert.Equal(0, initial.counter);
            Assert.Equal(witness.InitialRng, (initial.state0, initial.state1, initial.state2, initial.state3));
            Assert.Equal(sticky, monster.NextMove!.StateId);
            Assert.Equal(witness.Moves.Take(1), machine.StateLog.Select(state => state.Id));
            var expectedRng = rng.CloneExact();
            for (int turn = 0; turn < witness.Moves.Length - 1; turn++)
            {
                expectedRng.NextFloat();
                machine.OnMovePerformed(monster.NextMove!);
                monster.RollMove(room.Engine.State.Allies);
                Assert.True(monster.NextMove!.StateId == witness.Moves[turn + 1], $"seed={witness.Seed}, turn={turn}");
                Assert.Equal(witness.Moves.Take(turn + 2), machine.StateLog.Select(state => state.Id));
                Assert.Equal(expectedRng.ToSerializable(), rng.ToSerializable());
            }
        }
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
