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

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class AbundanceTests : IDisposable
{
    public AbundanceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_MatchesSource()
    {
        var card = (Abundance)ModelDb.Card<Abundance>().MutableClone();

        Assert.Equal(1, card.EnergyCost);
        Assert.Equal(CardType.Skill, card.Type);
        Assert.Equal(CardRarity.Ancient, card.Rarity);
        Assert.Equal(TargetType.Self, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Exhaust }, card.Keywords);
        Assert.False(card.CanBeGeneratedInCombat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Play_GeneratesThreeDistinctCandidates_AndAddsFreeFirstChoice(bool upgraded)
    {
        (Player player, _) = await CreateCombatAsync($"task11-abundance-{upgraded}");
        Abundance abundance = AddToHand<Abundance>(player);
        if (upgraded)
        {
            abundance.Upgrade();
        }

        await abundance.PlayAsync(target: null);

        Assert.Equal(3, abundance.GeneratedCandidates.Count);
        Assert.Equal(3, abundance.GeneratedCandidates.Select(card => card.GetType()).Distinct().Count());
        CardModel chosen = abundance.GeneratedCandidates[0];
        Assert.Contains(chosen, player.PlayerCombatState!.Hand.Cards);
        Assert.True(chosen.TemporaryFreeThisTurn);
        Assert.Equal(0, chosen.EnergyCost);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Play_AlwaysUpgradesGeneratedCandidates_RegardlessOfAbundancesOwnUpgradeState(bool upgraded)
    {
        // Authoritative MegaCrit.Sts2.Core.Models.Cards.Abundance.OnPlay unconditionally calls
        // CardCmd.Upgrade on every generated candidate; the candidates' upgrade state never
        // depends on whether the played Abundance card itself is upgraded.
        (Player player, _) = await CreateCombatAsync($"task11-abundance-candidates-always-upgraded-{upgraded}");
        Abundance abundance = AddToHand<Abundance>(player);
        if (upgraded)
        {
            abundance.Upgrade();
        }

        await abundance.PlayAsync(target: null);

        Assert.All(abundance.GeneratedCandidates, card => Assert.True(card.IsUpgraded));
    }

    [Fact]
    public async Task Play_ShufflesEntirePowerPoolFromCombatCardGeneration()
    {
        (Player player, var room) = await CreateCombatAsync("task11-abundance-rng-counter");
        Abundance abundance = AddToHand<Abundance>(player);
        int counterBefore = room.Engine.State.RunState.Rng.CombatCardGeneration.Counter;

        await abundance.PlayAsync(target: null);

        Assert.Equal(
            counterBefore + 15,
            room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
    }

    [Fact]
    public async Task Play_DifferentDeterministicSeedsSelectDifferentCandidateTypeSets()
    {
        (Player firstPlayer, _) = await CreateCombatAsync("task11-abundance-rng-seed-a");
        Abundance first = AddToHand<Abundance>(firstPlayer);
        await first.PlayAsync(target: null);
        var firstTypes = first.GeneratedCandidates.Select(card => card.GetType()).ToHashSet();

        (Player secondPlayer, _) = await CreateCombatAsync("task11-abundance-rng-seed-b");
        Abundance second = AddToHand<Abundance>(secondPlayer);
        await second.PlayAsync(target: null);
        var secondTypes = second.GeneratedCandidates.Select(card => card.GetType()).ToHashSet();

        Assert.False(firstTypes.SetEquals(secondTypes));
    }

    [Fact]
    public async Task Play_WhenHandRemainsFull_RoutesChosenCardToDiscard()
    {
        (Player player, _) = await CreateCombatAsync("task11-abundance-full");
        Abundance abundance = AddToHand<Abundance>(player);
        while (player.PlayerCombatState!.Hand.Cards.Count <= CardPile.MaxCardsInHand)
        {
            AddToHand<StrikeRegent>(player);
        }

        await abundance.PlayAsync(target: null);

        CardModel chosen = abundance.GeneratedCandidates[0];
        Assert.Contains(chosen, player.PlayerCombatState.DiscardPile.Cards);
        Assert.DoesNotContain(chosen, player.PlayerCombatState.Hand.Cards);
    }

    private static TCard AddToHand<TCard>(Player player) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
