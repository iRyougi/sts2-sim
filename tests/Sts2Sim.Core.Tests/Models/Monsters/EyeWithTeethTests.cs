namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Models.Afflictions;

[Collection("ModelDb")]
public sealed class EyeWithTeethTests : IDisposable
{
    private readonly Task16MonsterTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task FixedHpAndDistractIntent_MatchAuthoritativeSource()
    {
        (EyeWithTeeth eye, _, _) = await Task16MonsterTestFixture.CreateCombatAsync<EyeWithTeeth>(
            ascensionLevel: 10,
            seed: "eye-authoritative-metadata",
            slotName: "illusion");

        Assert.Equal(6, eye.MinInitialHp);
        Assert.Equal(6, eye.MaxInitialHp);
        Assert.Equal(6, eye.Creature.MaxHp);
        Assert.Equal("illusion", eye.Creature.SlotName);
        Assert.Equal("DISTRACT_MOVE", eye.NextMove!.StateId);
        StatusIntent intent = Assert.IsType<StatusIntent>(Assert.Single(eye.NextMove.Intents));
        Assert.Equal(3, intent.Count);
    }

    [Fact]
    public async Task Distract_SelfLoopsAndGeneratesThreeRealDazedForEveryPlayerEachTime()
    {
        (EyeWithTeeth eye, var room, IReadOnlyList<Player> players) =
            await Task16MonsterTestFixture.CreateCombatAsync<EyeWithTeeth>(
                ascensionLevel: 0,
                seed: "eye-distract-loop",
                playerCount: 2,
                slotName: "illusion",
                primaryCompanion: ((MonsterModel)ModelDb.Monster<Fogmog>().MutableClone(), "fogmog"));
        var ringing = new List<RingingPower>();
        foreach (Player player in players)
        {
            ringing.Add(Assert.IsType<RingingPower>(await PowerCmd.Apply<RingingPower>(
                room.Engine.State,
                player.Creature,
                1m,
                eye.Creature,
                cardSource: null)));
        }
        int[] generatedBefore = players
            .Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat)
            .ToArray();

        for (int cycle = 1; cycle <= 2; cycle++)
        {
            await Task16MonsterTestFixture.PerformAndRoll(eye, room);

            Assert.Equal("DISTRACT_MOVE", eye.NextMove!.StateId);
            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                Player player = players[playerIndex];
                Dazed[] dazed = player.PlayerCombatState!.DiscardPile.Cards.OfType<Dazed>().ToArray();
                Assert.Equal(cycle * 3, dazed.Length);
                Assert.Equal(generatedBefore[playerIndex] + (cycle * 3), player.PlayerCombatState.CardsGeneratedThisCombat);
                Assert.All(dazed, card =>
                {
                    Assert.Same(player, card.Owner);
                    Assert.Same(player.PlayerCombatState.DiscardPile, card.Pile);
                    Assert.Same(room.Engine.State, card.CombatState);
                    Assert.Contains(CardKeyword.Ethereal, card.Keywords);
                    Assert.Contains(CardKeyword.Unplayable, card.Keywords);
                    Assert.True(card.Affliction is Ringing);
                });
            }
        }
    }

    [Fact]
    public async Task InitialCombat_AfterAddedLifecycleRunsOnceAndInstallsIllusionAndMinion()
    {
        (EyeWithTeeth eye, _, _) = await Task16MonsterTestFixture.CreateCombatAsync<EyeWithTeeth>(
            ascensionLevel: 0,
            seed: "eye-after-added-initial",
            slotName: "illusion");

        IllusionPower illusion = Assert.IsType<IllusionPower>(eye.Creature.GetPower<IllusionPower>());
        MinionPower minion = Assert.IsType<MinionPower>(eye.Creature.GetPower<MinionPower>());
        Assert.Equal(1, illusion.Amount);
        Assert.Equal(1, minion.Amount);
        Assert.Equal(new[] { "DISTRACT_MOVE" }, eye.MoveStateMachine!.StateLog.Select(state => state.Id));
        Assert.Equal("DISTRACT_MOVE", eye.NextMove!.StateId);
    }

    [Fact]
    public async Task IllusionDeath_RevivesAtFullHpThenReturnsToDistractOnTheFollowingEnemyTurn()
    {
        (EyeWithTeeth eye, var room, IReadOnlyList<Player> players) =
            await Task16MonsterTestFixture.CreateCombatAsync<EyeWithTeeth>(
                ascensionLevel: 0,
                seed: "eye-revive-production-path",
                playerCount: 2,
                slotName: "illusion");
        IllusionPower illusion = Assert.IsType<IllusionPower>(eye.Creature.GetPower<IllusionPower>());
        await CreatureCmd.Add(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            room.Engine.State,
            Sts2Sim.Core.Combat.CombatSide.Enemy,
            "primary");

        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { eye.Creature },
            eye.Creature.CurrentHp,
            ValueProp.Unblockable,
            dealer: players[0].Creature,
            cardSource: null,
            cardPlay: null);

        Assert.True(eye.Creature.IsDead);
        Assert.True(illusion.IsReviving);
        Assert.Equal("REVIVE_MOVE", eye.NextMove!.StateId);
        Assert.NotNull(eye.Creature.GetPower<IllusionPower>());
        Assert.NotNull(eye.Creature.GetPower<MinionPower>());
        Assert.False(room.Engine.CheckWinCondition());

        await room.Engine.EndPlayerTurnAsync();

        Assert.True(eye.Creature.IsAlive);
        Assert.Equal(eye.Creature.MaxHp, eye.Creature.CurrentHp);
        Assert.False(illusion.IsReviving);
        Assert.Equal("DISTRACT_MOVE", eye.NextMove!.StateId);
        Assert.True(room.Engine.IsInProgress);
        Assert.All(players, player =>
            Assert.Empty(player.PlayerCombatState!.DiscardPile.Cards.OfType<Dazed>()));

        await room.Engine.EndPlayerTurnAsync();

        Assert.All(players, player =>
            Assert.Equal(
                3,
                player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).OfType<Dazed>().Count()));
        Assert.Equal("DISTRACT_MOVE", eye.NextMove!.StateId);
    }
}
