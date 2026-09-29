using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Combat;

[Collection("ModelDb")]
public sealed class CombatEngineCardPlayResultTests : IDisposable
{
    public CombatEngineCardPlayResultTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task PlayCardAsync_ReturnsResolvedNormalEnergySpend()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("card-result-normal");
        StrikeRegent card = AddToHand<StrikeRegent>(player);

        CardPlay? result = await room.Engine.PlayCardWithResultAsync(
            player,
            card,
            room.Engine.State.HittableEnemies.Single());

        CardPlay play = Assert.IsType<CardPlay>(result);
        Assert.Equal(1, play.Resources.EnergySpent);
        Assert.Equal(1, play.Resources.EnergyValue);
        Assert.False(play.IsAutoPlay);
    }

    [Fact]
    public async Task PlayCardAsync_ReturnsResolvedXEnergySpend()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("card-result-x");
        Volley card = AddToHand<Volley>(player);
        player.PlayerCombatState!.Energy = 3;

        CardPlay? result = await room.Engine.PlayCardWithResultAsync(
            player,
            card,
            room.Engine.State.HittableEnemies.Single());

        CardPlay play = Assert.IsType<CardPlay>(result);
        Assert.Equal(3, play.Resources.EnergySpent);
        Assert.Equal(3, play.Resources.EnergyValue);
    }

    [Fact]
    public async Task PlayCardAsync_ReturnsZeroSpendForTemporarilyFreeCard()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("card-result-free");
        StrikeRegent card = AddToHand<StrikeRegent>(player);
        card.MakeTemporaryFreeThisTurn();

        CardPlay? result = await room.Engine.PlayCardWithResultAsync(
            player,
            card,
            room.Engine.State.HittableEnemies.Single());

        CardPlay play = Assert.IsType<CardPlay>(result);
        Assert.Equal(0, play.Resources.EnergySpent);
        Assert.False(play.IsAutoPlay);
    }

    [Fact]
    public async Task FromTopOfDrawPile_ReturnsResolvedAutoplayWithoutSpendingEnergy()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("card-result-autoplay");
        ClearCombatPiles(player);
        Volley card = AddTo<Volley>(player, PileType.Draw, CardPilePosition.Top);
        player.PlayerCombatState!.Energy = 3;

        IReadOnlyList<CardPlay> results = await AutoPlayCmd.FromTopOfDrawPileWithResults(
            room.Engine.State,
            player,
            count: 1);

        CardPlay play = Assert.Single(results);
        Assert.Same(card, play.Card);
        Assert.True(play.IsAutoPlay);
        Assert.Equal(0, play.Resources.EnergySpent);
        Assert.Equal(3, play.Resources.EnergyValue);
        Assert.Equal(3, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task PlayCardAsync_WithReplay_ReturnsFirstPlayAndGrossSpendOnce()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("card-result-replay");
        DefendRegent card = AddToHand<DefendRegent>(player);
        card.BaseReplayCount = 2;

        CardPlay? result = await room.Engine.PlayCardWithResultAsync(player, card, target: null);

        CardPlay play = Assert.IsType<CardPlay>(result);
        Assert.Equal(0, play.PlayIndex);
        Assert.Equal(3, play.PlayCount);
        Assert.Equal(1, play.Resources.EnergySpent);
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel =>
        AddTo<TCard>(player, PileType.Hand, CardPilePosition.Bottom);

    private static TCard AddTo<TCard>(
        Player player,
        PileType pileType,
        CardPilePosition position)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType, position);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        PlayerCombatState state = player.PlayerCombatState!;
        foreach (CardModel card in state.Hand.Cards
                     .Concat(state.DrawPile.Cards)
                     .Concat(state.DiscardPile.Cards)
                     .ToArray())
        {
            CardPileCmd.Remove(card);
        }
    }
}
