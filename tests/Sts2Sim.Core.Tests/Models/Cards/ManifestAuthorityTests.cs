using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
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
public sealed class ManifestAuthorityTests : IDisposable
{
    public ManifestAuthorityTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ModelTypes());
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Play_GainsSevenBlockAndGeneratesOneEligibleColorlessCard_UsingCombatGenerationRng()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("manifest-base");
        ManifestAuthority manifest = AddToHand<ManifestAuthority>(player);

        Assert.Equal(1, manifest.EnergyCost);
        await manifest.PlayAsync(target: null);

        ManifestEligibleCard generated = Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<ManifestEligibleCard>());
        Assert.Equal(7, player.Creature.Block);
        Assert.Same(player, generated.Owner);
        Assert.Same(player.PlayerCombatState.Hand, generated.Pile);
        Assert.False(generated.IsUpgraded);
        Assert.Equal(2, generated.EnergyCost);
        // Native full-pool shuffle needs no draw when only one candidate is eligible.
        Assert.Equal(0, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
        Assert.Equal(0, room.Engine.State.RunState.Rng.CombatCardSelection.Counter);
        Assert.Equal(
            new[] { typeof(ManifestEligibleCard) },
            CardPoolFilters.ForCombatGeneration(ModelDb.All<CardModel>())
                .Where(card => card.IsColorless)
                .Select(card => card.GetType())
                .OrderBy(type => type.Name)
                .ToArray());
    }

    [Fact]
    public async Task Play_WhenUpgraded_GainsEightBlockAndUpgradesOnlyTheGeneratedClone()
    {
        (Player player, _) = await CreateCombatAsync("manifest-upgrade");
        ManifestAuthority manifest = AddToHand<ManifestAuthority>(player);
        CardModel canonical = ModelDb.Card<ManifestEligibleCard>();
        manifest.Upgrade();

        await manifest.PlayAsync(target: null);

        ManifestEligibleCard generated = Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<ManifestEligibleCard>());
        Assert.Equal(8, player.Creature.Block);
        Assert.True(generated.IsUpgraded);
        Assert.Equal(1, generated.CurrentUpgradeLevel);
        Assert.False(canonical.IsUpgraded);
        Assert.Equal(2, canonical.EnergyCost);
    }

    [Fact]
    public async Task Play_AtFullHand_RedirectsGeneratedCardToDiscardAndTriggersPillar()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("manifest-full-hand");
        await PowerCmd.Apply<PillarOfCreationPower>(room.Engine.State, player.Creature, 5m, player.Creature, null);
        ManifestAuthority manifest = AddToHand<ManifestAuthority>(player);
        while (player.PlayerCombatState!.Hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            AddToHand<ManifestEligibleCard>(player);
        }
        AddToHand<ManifestEligibleCard>(player);

        await manifest.PlayAsync(target: null);

        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState!.Hand.Cards.Count);
        ManifestEligibleCard generated = Assert.Single(player.PlayerCombatState.DiscardPile.Cards.OfType<ManifestEligibleCard>());
        Assert.Same(player, generated.Owner);
        Assert.Same(player.PlayerCombatState.DiscardPile, generated.Pile);
        Assert.Equal(12, player.Creature.Block);
    }

    [Fact]
    public async Task Play_IsDeterministicForTheDedicatedGenerationStream()
    {
        (string first, int firstGenerationCounter, int firstSelectionCounter) = await PlayAndCaptureAsync("manifest-rng");
        (string second, int secondGenerationCounter, int secondSelectionCounter) = await PlayAndCaptureAsync("manifest-rng");

        Assert.Equal(first, second);
        Assert.Equal(0, firstGenerationCounter);
        Assert.Equal(0, secondGenerationCounter);
        Assert.Equal(0, firstSelectionCounter);
        Assert.Equal(0, secondSelectionCounter);
    }

    private static IEnumerable<Type> ModelTypes() =>
    [
        typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
        typeof(WanderingGrunt), typeof(SovereignBlade), typeof(DivineRight),
        typeof(ManifestAuthority), typeof(PillarOfCreationPower),
        typeof(ManifestEligibleCard), typeof(ManifestNonColorlessCard),
        typeof(ManifestBasicCard), typeof(ManifestAncientCard), typeof(ManifestEventCard),
        typeof(ManifestMultiplayerCard), typeof(ManifestCannotGenerateCard),
    ];

    private static async Task<(string generated, int generationCounter, int selectionCounter)> PlayAndCaptureAsync(string seed)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync(seed);
        ManifestAuthority manifest = AddToHand<ManifestAuthority>(player);

        await manifest.PlayAsync(target: null);

        return (
            player.PlayerCombatState!.Hand.Cards.OfType<ManifestTestCard>().Single().GetType().Name,
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

[Collection("ModelDb")]
public sealed class ManifestAuthorityEmptyPoolTests : IDisposable
{
    public ManifestAuthorityEmptyPoolTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WanderingGrunt), typeof(SovereignBlade), typeof(DivineRight), typeof(ManifestAuthority),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Play_WhenNoEligibleColorlessCardExists_GainsBlockWithoutDrawingGenerationRng()
    {
        var runState = new RunState("manifest-empty", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        var manifest = (ManifestAuthority)ModelDb.Card<ManifestAuthority>().MutableClone();
        manifest.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(manifest);

        await manifest.PlayAsync(target: null);

        Assert.Equal(7, player.Creature.Block);
        Assert.Empty(player.PlayerCombatState.Hand.Cards.OfType<ManifestTestCard>());
        Assert.Empty(player.PlayerCombatState.DiscardPile.Cards.OfType<ManifestTestCard>());
        Assert.Contains(manifest, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(0, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
    }
}

public sealed class ManifestEligibleCard : ManifestTestCard
{
    protected override int CanonicalEnergyCost => 2;
}

public sealed class ManifestOtherEligibleCard : ManifestTestCard
{
    protected override int CanonicalEnergyCost => 1;
}

public sealed class ManifestNonColorlessCard : ManifestTestCard
{
    public override bool IsColorless => false;
}

public sealed class ManifestBasicCard : ManifestTestCard
{
    public override CardRarity Rarity => CardRarity.Basic;
}

public sealed class ManifestAncientCard : ManifestTestCard
{
    public override CardRarity Rarity => CardRarity.Ancient;
}

public sealed class ManifestEventCard : ManifestTestCard
{
    public override CardRarity Rarity => CardRarity.Event;
}

public sealed class ManifestMultiplayerCard : ManifestTestCard
{
    public override bool IsMultiplayerOnly => true;
}

public sealed class ManifestCannotGenerateCard : ManifestTestCard
{
    public override bool CanBeGeneratedInCombat => false;
}

public abstract class ManifestTestCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;
}
