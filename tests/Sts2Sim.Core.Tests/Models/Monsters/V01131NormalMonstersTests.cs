namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;

/// <summary>#925: v0.113.1 normal-monster changes at the A7–A10 ascension boundaries.</summary>
[Collection("ModelDb")]
public sealed class V01131NormalMonstersTests : IDisposable
{
    private readonly Task20BossTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(7, 70, 24, 1)]
    [InlineData(8, 80, 24, 1)]
    [InlineData(9, 80, 28, 2)]
    [InlineData(10, 80, 28, 2)]
    public async Task NormalMonsters_MatchV01131AcrossAscensionBoundary(
        int ascension, int axebotHp, int fromBelowDamage, int witherCount)
    {
        // Axebot: fixed initial HP (Max == Min), respawn bonus added on top.
        (Axebot axebot, _, _) = await Task20BossTestFixture.CreateSingleCombatAsync<Axebot>(ascension, $"925-axe-{ascension}");
        Assert.Equal(axebotHp, axebot.MinInitialHp);
        Assert.Equal(axebotHp, axebot.MaxInitialHp);
        Assert.Equal(axebotHp, axebot.Creature.MaxHp);

        // Tunneler: renamed FROM_BELOW_MOVE with 24(28) damage.
        (Tunneler tunneler, _, _) = await Task20BossTestFixture.CreateSingleCombatAsync<Tunneler>(ascension, $"925-tun-{ascension}");
        SingleAttackIntent fromBelow = Assert.IsType<SingleAttackIntent>(
            Assert.Single(Assert.IsType<MoveState>(tunneler.MoveStateMachine!.States["FROM_BELOW_MOVE"]).Intents));
        Assert.Equal(fromBelowDamage, fromBelow.GetSingleDamage(tunneler.Creature.CombatState!.Allies, tunneler.Creature));

        // Aeonglass: each generated Wither is inserted at Rng.Shuffle.NextInt(discardCount + 1).
        (Aeonglass aeonglass, _, IReadOnlyList<Player> aeonPlayers) =
            await Task20BossTestFixture.CreateSingleCombatAsync<Aeonglass>(ascension, $"925-aeon-{ascension}");
        PlayerCombatState aeonState = aeonPlayers[0].PlayerCombatState!;
        foreach (CardModel card in aeonState.DrawPile.Cards.Take(4).ToArray())
        {
            CardPileCmd.Remove(card);
            CardPileCmd.Add(card, PileType.Discard);
        }
        var expected = aeonState.DiscardPile.Cards.Cast<CardModel?>().ToList();
        Rng shuffleOracle = aeonPlayers[0].RunState.Rng.Shuffle.CloneExact();
        for (int index = 0; index < witherCount; index++)
            expected.Insert(shuffleOracle.NextInt(expected.Count + 1), null);
        Task20BossTestFixture.ForceMove(aeonglass, "INCREASING_INTENSITY_MOVE");
        await aeonglass.PerformMove();
        Assert.Equal(shuffleOracle.Counter, aeonPlayers[0].RunState.Rng.Shuffle.Counter);
        Assert.Equal(
            expected.Select(card => card is null ? "Wither" : card.GetType().Name),
            aeonState.DiscardPile.Cards.Select(card => card is Wither ? "Wither" : card.GetType().Name));

        // ThievingHopper: Quest cards are never stolen, even when they are the only candidate.
        (ThievingHopper hopper, _, IReadOnlyList<Player> hopperPlayers) =
            await Task20BossTestFixture.CreateSingleCombatAsync<ThievingHopper>(ascension, $"925-hop-{ascension}");
        Player hopperPlayer = hopperPlayers[0];
        foreach (CardModel card in hopperPlayer.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToArray())
            CardPileCmd.Remove(card);
        var quest = (CardModel)ModelDb.Card<Dowsing>().MutableClone();
        Assert.Equal(CardRarity.Quest, quest.Rarity);
        quest.AssignOwner(hopperPlayer);
        quest.AssignDeckVersionInternal(hopperPlayer.Deck.Cards[0]);
        CardPileCmd.Add(quest, PileType.Draw);
        Assert.Equal("THIEVERY_MOVE", hopper.NextMove!.StateId);
        await hopper.PerformMove();
        Assert.Same(quest, Assert.Single(hopperPlayer.PlayerCombatState.DrawPile.Cards));
        Assert.Null(hopper.Creature.GetPower<SwipePower>());
    }
}
