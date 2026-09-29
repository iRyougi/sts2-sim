using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class JackOfAllTradesHandLimitTests : IDisposable
{
    public JackOfAllTradesHandLimitTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WanderingGrunt), typeof(SovereignBlade), typeof(DivineRight),
            typeof(JackOfAllTrades), typeof(PillarOfCreationPower), typeof(JackReviewGenerationCountingPower),
            typeof(JackEligibleAlpha), typeof(JackEligibleBeta),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Play_WhenUpgradedAtHandLimit_RedirectsSecondGenerationToDiscard_AndDispatchesBothHooks()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("jack-full-hand");
        await PowerCmd.Apply<PillarOfCreationPower>(room.Engine.State, player.Creature, 5m, player.Creature, null);
        JackReviewGenerationCountingPower counting = (await PowerCmd.Apply<JackReviewGenerationCountingPower>(
            room.Engine.State, player.Creature, 1m, player.Creature, null))!;
        JackOfAllTrades jack = AddToHand<JackOfAllTrades>(player);
        jack.Upgrade();
        while (player.PlayerCombatState!.Hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            AddToHand<JackEligibleAlpha>(player);
        }

        await jack.PlayAsync(target: null);

        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState!.Hand.Cards.Count);
        CardModel redirected = Assert.Single(player.PlayerCombatState.DiscardPile.Cards);
        Assert.IsAssignableFrom<JackTestCard>(redirected);
        Assert.Same(player, redirected.Owner);
        Assert.Same(player.PlayerCombatState.DiscardPile, redirected.Pile);
        Assert.Equal(2, counting.GeneratedCount);
        Assert.Equal(5, player.Creature.Block);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
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
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

[Collection("ModelDb")]
public sealed class JackOfAllTradesNoEligibleTests : IDisposable
{
    public JackOfAllTradesNoEligibleTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(BaseModelTypes());
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Play_WhenNoEligibleCardExists_ExhaustsWithoutGeneratingOrConsumingRng()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("jack-empty");
        JackOfAllTrades jack = AddToHand<JackOfAllTrades>(player);
        jack.Upgrade();

        await jack.PlayAsync(target: null);

        Assert.Empty(player.PlayerCombatState!.Hand.Cards.OfType<JackTestCard>());
        Assert.Empty(player.PlayerCombatState.DiscardPile.Cards);
        Assert.Contains(jack, player.PlayerCombatState.ExhaustPile.Cards);
        Assert.Equal(0, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
    }

    private static IEnumerable<Type> BaseModelTypes() =>
    [
        typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
        typeof(WanderingGrunt), typeof(SovereignBlade), typeof(DivineRight), typeof(JackOfAllTrades),
    ];

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
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
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

[Collection("ModelDb")]
public sealed class JackOfAllTradesOneEligibleTests : IDisposable
{
    public JackOfAllTradesOneEligibleTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WanderingGrunt), typeof(SovereignBlade), typeof(DivineRight),
            typeof(JackOfAllTrades), typeof(JackEligibleAlpha),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Play_WhenOnlyOneEligibleCardExists_GeneratesItOnceWithCorrectOwnerAndPile()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("jack-one-candidate");
        JackOfAllTrades jack = AddToHand<JackOfAllTrades>(player);
        jack.Upgrade();

        await jack.PlayAsync(target: null);

        JackEligibleAlpha generated = Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<JackEligibleAlpha>());
        Assert.Same(player, generated.Owner);
        Assert.Same(player.PlayerCombatState.Hand, generated.Pile);
        Assert.Empty(player.PlayerCombatState.DiscardPile.Cards);
        Assert.Contains(jack, player.PlayerCombatState.ExhaustPile.Cards);
        Assert.Equal(0, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
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
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

public sealed class JackReviewGenerationCountingPower : PowerModel
{
    public int GeneratedCount { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterCardGenerated(CardModel card)
    {
        GeneratedCount++;
        return Task.CompletedTask;
    }
}
