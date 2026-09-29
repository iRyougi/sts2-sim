using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Commands;

[Collection("ModelDb")]
public sealed class CardCmdTransformToTests : IDisposable
{
    public CardCmdTransformToTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight),
            typeof(Greed),
            typeof(Peck),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task TransformTo_NullSource_ThrowsBeforeAnyMutation()
    {
        (RunState runState, Player player) = Setup("transform-to-null");
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CardCmd.TransformTo<Peck>(null!, runState));

        Assert.Equal(deckBefore, player.Deck.Cards);
    }

    [Fact]
    public async Task TransformTo_NullRunState_ThrowsBeforeAnyMutation()
    {
        (_, Player player) = Setup("transform-to-null-run");
        CardModel source = player.Deck.Cards[0];
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CardCmd.TransformTo<Peck>(source, null!));

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Same(player.Deck, source.Pile);
    }
    [Fact]
    public async Task TransformTo_CardFromAnotherRun_ThrowsBeforeAnyMutation()
    {
        (_, Player player) = Setup("transform-to-owner-run");
        var otherRun = new RunState("transform-to-other-run", new Overgrowth());
        CardModel source = player.Deck.Cards[0];
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformTo<Peck>(source, otherRun));

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Same(player.Deck, source.Pile);
    }
    [Fact]
    public async Task TransformTo_CardWithoutPile_ThrowsBeforeAnyMutation()
    {
        (RunState runState, Player player) = Setup("transform-to-no-pile");
        var source = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        source.AssignOwner(player);
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformTo<Peck>(source, runState));

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Null(source.Pile);
    }
    [Fact]
    public async Task TransformTo_NonTransformableCard_IsRejectedBeforeTargetLookupOrMutation()
    {
        (RunState runState, Player player) = Setup("transform-to-eternal");
        var source = (Greed)ModelDb.Card<Greed>().MutableClone();
        source.AssignOwner(player);
        CardPileCmd.Add(source, PileType.Deck);
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformTo<Infection>(source, runState));

        Assert.Contains("Eternal", exception.Message);
        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Same(player.Deck, source.Pile);
    }
    [Fact]
    public async Task TransformTo_MissingTargetCanonical_ThrowsBeforeAnyMutation()
    {
        (RunState runState, Player player) = Setup("transform-to-missing-target");
        CardModel source = player.Deck.Cards[0];
        CardModel[] deckBefore = player.Deck.Cards.ToArray();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.TransformTo<Infection>(source, runState));

        Assert.Contains("registered", exception.Message);
        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Same(player.Deck, source.Pile);
    }
    [Fact]
    public async Task TransformTo_ReplacesSourceWithOwnedTargetInTheSamePile()
    {
        (RunState runState, Player player) = Setup("transform-to-success");
        CardModel source = player.Deck.Cards[0];
        CardPile sourcePile = source.Pile!;

        Peck target = await CardCmd.TransformTo<Peck>(source, runState);

        Assert.IsType<Peck>(target);
        Assert.Same(player, target.Owner);
        Assert.Same(sourcePile, target.Pile);
        Assert.Contains(target, sourcePile.Cards);
        Assert.DoesNotContain(sourcePile.Cards, card => ReferenceEquals(card, source));
        Assert.Null(source.Pile);
    }

    private static (RunState RunState, Player Player) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
