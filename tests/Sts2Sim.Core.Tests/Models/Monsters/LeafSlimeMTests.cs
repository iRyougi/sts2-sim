namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Models.Afflictions;

[Collection("ModelDb")]
public sealed class LeafSlimeMTests : IDisposable
{
    private readonly Task15MonsterTestFixture _fixture = new(typeof(LeafSlimeM));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 32, 35)]
    [InlineData((int)AscensionLevel.ToughEnemies, 33, 36)]
    public async Task HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (LeafSlimeM monster, _, _) = await Task15MonsterTestFixture.CreateCombatAsync<LeafSlimeM>(
            ascension,
            $"leaf-slime-m-hp-{ascension}",
            playerCount: 1);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 9)]
    public async Task StickyThenClump_CompletesTheAuthoritativeLoopAndEffectsForEveryPlayer(
        int ascension,
        int expectedDamage)
    {
        (LeafSlimeM monster, var room, IReadOnlyList<Player> players) =
            await Task15MonsterTestFixture.CreateCombatAsync<LeafSlimeM>(
                ascension,
                $"leaf-slime-m-loop-{ascension}");
        IReadOnlyList<RingingPower> ringing = await Task15MonsterTestFixture.ApplyRingingAsync(room, players);
        int[] generatedBefore = players
            .Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat)
            .ToArray();

        Assert.Equal("STICKY_SHOT", monster.NextMove!.StateId);
        StatusIntent stickyIntent = Assert.IsType<StatusIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(2, stickyIntent.Count);

        await Task15MonsterTestFixture.PerformAndRoll(monster, room);

        Assert.Equal("CLUMP_SHOT", monster.NextMove!.StateId);
        for (int index = 0; index < players.Count; index++)
        {
            Player player = players[index];
            Slimed[] generated = player.PlayerCombatState!.DiscardPile.Cards.OfType<Slimed>().ToArray();
            Assert.Equal(2, generated.Length);
            Assert.Equal(generatedBefore[index], player.PlayerCombatState.CardsGeneratedThisCombat);
            Assert.All(generated, card =>
            {
                Assert.Same(player, card.Owner);
                Assert.Same(player.PlayerCombatState.DiscardPile, card.Pile);
                Assert.Same(room.Engine.State, card.CombatState);
                Assert.True(card.Affliction is Ringing);
            });
        }

        SingleAttackIntent clumpIntent = Assert.IsType<SingleAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(expectedDamage, clumpIntent.GetSingleDamage(room.Engine.State.Allies, monster.Creature));
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();

        await Task15MonsterTestFixture.PerformAndRoll(monster, room);

        Assert.Equal("STICKY_SHOT", monster.NextMove!.StateId);
        Assert.Equal(hpBefore.Select(hp => hp - expectedDamage), players.Select(player => player.Creature.CurrentHp));
        Assert.Equal(
            generatedBefore,
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));
    }
}
