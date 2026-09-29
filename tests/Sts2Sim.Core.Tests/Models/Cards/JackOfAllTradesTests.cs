using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
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
public sealed class JackOfAllTradesTests : IDisposable
{
    public JackOfAllTradesTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WanderingGrunt), typeof(SovereignBlade), typeof(DivineRight),
            typeof(JackOfAllTrades), typeof(PillarOfCreationPower),
            typeof(JackEligibleAlpha), typeof(JackEligibleBeta), typeof(JackNonColorless),
            typeof(JackBasic), typeof(JackAncient), typeof(JackEvent), typeof(JackMultiplayerOnly),
            typeof(JackCannotGenerate),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Play_GeneratesOneEligibleColorlessCardIntoHand_AndExhausts()
    {
        (Player player, _) = await CreateCombatAsync("jack-base");
        JackOfAllTrades jack = AddToHand<JackOfAllTrades>(player);

        await jack.PlayAsync(target: null);

        CardModel generated = Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<JackTestCard>());
        Assert.Same(player, generated.Owner);
        Assert.Same(player.PlayerCombatState.Hand, generated.Pile);
        Assert.True(generated.IsColorless);
        Assert.False(generated.IsUpgraded);
        Assert.InRange(generated.EnergyCost, 1, 2);
        Assert.NotEqual(typeof(JackOfAllTrades), generated.GetType());
        Assert.Contains(generated, CardPoolFilters.ForCombatGeneration(ModelDb.All<CardModel>()));
        Assert.Contains(jack, player.PlayerCombatState.ExhaustPile.Cards);
    }

    [Fact]
    public async Task Play_WhenUpgraded_GeneratesTwoDistinctEligibleCards_AndExhausts()
    {
        (Player player, _) = await CreateCombatAsync("jack-upgraded");
        JackOfAllTrades jack = AddToHand<JackOfAllTrades>(player);
        jack.Upgrade();

        await jack.PlayAsync(target: null);

        CardModel[] generated = player.PlayerCombatState!.Hand.Cards.OfType<JackTestCard>().Cast<CardModel>().ToArray();
        Assert.Equal(2, generated.Length);
        Assert.Equal(2, generated.Select(card => card.GetType()).Distinct().Count());
        Assert.All(generated, card =>
        {
            Assert.True(card.IsColorless);
            Assert.False(card.IsUpgraded);
            Assert.NotEqual(typeof(JackOfAllTrades), card.GetType());
            Assert.Same(player, card.Owner);
            Assert.Same(player.PlayerCombatState.Hand, card.Pile);
            Assert.Contains(card, CardPoolFilters.ForCombatGeneration(ModelDb.All<CardModel>()));
        });
        Assert.Contains(jack, player.PlayerCombatState.ExhaustPile.Cards);
    }

    [Fact]
    public async Task Play_UsesCombatCardGenerationStream_Deterministically()
    {
        (string[] first, int firstGenerationCounter, int firstSelectionCounter) = await PlayAndCaptureAsync("jack-rng");
        (string[] second, int secondGenerationCounter, int secondSelectionCounter) = await PlayAndCaptureAsync("jack-rng");

        Assert.Equal(first, second);
        Assert.Equal(1, firstGenerationCounter);
        Assert.Equal(1, secondGenerationCounter);
        Assert.Equal(0, firstSelectionCounter);
        Assert.Equal(0, secondSelectionCounter);
    }

    [Fact]
    public async Task Play_TriggersPillarOfCreationOnlyForTheFirstGeneratedCard_WhenUpgraded()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("jack-pillar");
        await PowerCmd.Apply<PillarOfCreationPower>(room.Engine.State, player.Creature, 5m, player.Creature, null);
        JackOfAllTrades jack = AddToHand<JackOfAllTrades>(player);
        int handCountBefore = player.PlayerCombatState!.Hand.Cards.Count;
        jack.Upgrade();

        await jack.PlayAsync(target: null);

        Assert.Equal(handCountBefore + 1, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Equal(5, player.Creature.Block);
    }

    private static async Task<(string[] generated, int generationCounter, int selectionCounter)> PlayAndCaptureAsync(string seed)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync(seed);
        JackOfAllTrades jack = AddToHand<JackOfAllTrades>(player);

        await jack.PlayAsync(target: null);

        return (
            player.PlayerCombatState!.Hand.Cards.OfType<JackTestCard>().Select(card => card.GetType().Name).ToArray(),
            room.Engine.State.RunState.Rng.CombatCardGeneration.Counter,
            room.Engine.State.RunState.Rng.CombatCardSelection.Counter);
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

public sealed class JackEligibleAlpha : JackTestCard
{
    protected override int CanonicalEnergyCost => 2;
}

public sealed class JackEligibleBeta : JackTestCard
{
    protected override int CanonicalEnergyCost => 1;
}

public sealed class JackNonColorless : JackTestCard
{
    public override bool IsColorless => false;
}

public sealed class JackBasic : JackTestCard
{
    public override CardRarity Rarity => CardRarity.Basic;
}

public sealed class JackAncient : JackTestCard
{
    public override CardRarity Rarity => CardRarity.Ancient;
}

public sealed class JackEvent : JackTestCard
{
    public override CardRarity Rarity => CardRarity.Event;
}

public sealed class JackMultiplayerOnly : JackTestCard
{
    public override bool IsMultiplayerOnly => true;
}

public sealed class JackCannotGenerate : JackTestCard
{
    public override bool CanBeGeneratedInCombat => false;
}

public abstract class JackTestCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;
}
