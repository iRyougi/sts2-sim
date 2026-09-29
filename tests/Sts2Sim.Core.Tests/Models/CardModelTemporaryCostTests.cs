using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class CardModelTemporaryCostTests : IDisposable
{
    public CardModelTemporaryCostTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(DivineRight), typeof(WanderingGrunt), typeof(TemporaryCostReplayCard),
            typeof(ThrowingHandDepartureCard), typeof(TemporaryFixedStarCostCard), typeof(Stardust),
            typeof(TangledPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task MakeFreeUntilPlayed_SurvivesTurnCleanup_AndClearsAfterWholePlay()
    {
        (Player player, _) = await CreateCombatAsync("free-until-played");
        TemporaryCostReplayCard card = AddToHand<TemporaryCostReplayCard>(player);
        card.BaseReplayCount = 1;
        card.MakeFreeUntilPlayed();
        int energyBefore = player.PlayerCombatState!.Energy;

        Assert.Equal(0, card.EnergyCost);
        player.PlayerCombatState.EndOfTurnCleanup();
        Assert.Equal(0, card.EnergyCost);

        await card.PlayAsync(target: null);

        Assert.Equal(new[] { 0, 0 }, card.ObservedEnergyCosts);
        Assert.Equal(energyBefore, player.PlayerCombatState.Energy);
        Assert.False(card.TemporaryFreeUntilPlayed);
        Assert.Equal(1, card.EnergyCost);

        TemporaryCostReplayCard accumulating = AddToHand<TemporaryCostReplayCard>(player);
        accumulating.BaseReplayCount = 1;
        accumulating.AddEnergyCostThisCombat(2);
        accumulating.AddEnergyCostUntilPlayed(-1);
        accumulating.AddEnergyCostUntilPlayed(-1);
        Assert.Equal(1, accumulating.LocalEnergyCost);
        player.PlayerCombatState.EndOfTurnCleanup();
        Assert.Equal(1, accumulating.EnergyCost); // Until-played modifiers survive turn cleanup.
        CardModel copy = accumulating.CreateClone();
        Assert.Equal(1, copy.EnergyCost);

        await accumulating.PlayAsync(target: null);

        Assert.Equal(new[] { 1, 1 }, accumulating.ObservedEnergyCosts);
        Assert.Equal(3, accumulating.EnergyCost); // One cleanup after the whole replay series.
        Assert.Equal(1, copy.EnergyCost); // Cloned modifier list is independent.
    }

    [Fact]
    public async Task MakeFreeUntilPlayed_WhenFinalHandDepartureHookThrows_RemainsFree()
    {
        (Player player, _) = await CreateCombatAsync("free-until-played-failure");
        PlayerCombatState combatState = player.PlayerCombatState!;
        foreach (CardModel existingCard in combatState.Hand.Cards.ToList())
        {
            CardPileCmd.Add(existingCard, PileType.Discard);
        }
        ThrowingHandDepartureCard card = AddToHand<ThrowingHandDepartureCard>(player);
        card.MakeFreeUntilPlayed();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => card.PlayAsync(target: null));

        Assert.Equal("hand-departure failure", exception.Message);
        Assert.True(card.TemporaryFreeUntilPlayed);
        Assert.Equal(0, card.EnergyCost);
    }

    [Fact]
    public async Task MakeTemporaryFreeThisTurn_ZeroesFixedStarCostAndDoesNotSpendStars()
    {
        (Player player, _) = await CreateCombatAsync("temporary-fixed-stars");
        TemporaryFixedStarCostCard card = AddToHand<TemporaryFixedStarCostCard>(player);
        player.PlayerCombatState!.GainStars(3);
        int starsBefore = player.PlayerCombatState.Stars;

        card.MakeTemporaryFreeThisTurn();

        Assert.Equal(0, card.StarCost);
        await card.PlayAsync(target: null);
        Assert.Equal(starsBefore, player.PlayerCombatState.Stars);
    }

    [Fact]
    public async Task MakeTemporaryFreeThisTurn_ZeroesXStarCostAndDoesNotSpendStars()
    {
        (Player player, _) = await CreateCombatAsync("temporary-x-stars");
        Stardust card = AddToHand<Stardust>(player);
        player.PlayerCombatState!.GainStars(3);
        int starsBefore = player.PlayerCombatState.Stars;

        card.MakeTemporaryFreeThisTurn();

        Assert.Equal(0, card.StarCost);
        await card.PlayAsync(target: null);
        Assert.Equal(starsBefore, player.PlayerCombatState.Stars);
    }

    [Fact]
    public async Task TemporaryCostOverride_HasPrecedenceOverFreeFlags_AndControlsEnergySpent()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("override-precedence");
        DefendRegent card = AddToHand<DefendRegent>(player);
        card.MakeTemporaryFreeThisTurn();
        card.MakeFreeUntilPlayed();
        card.SetTemporaryCostOverrideThisTurn(2);
        int energyBefore = player.PlayerCombatState!.Energy;

        Assert.Equal(2, card.EnergyCost);
        await card.PlayAsync(target: null);

        Assert.Equal(energyBefore - 2, player.PlayerCombatState.Energy);
        Assert.False(card.TemporaryFreeUntilPlayed);
        Assert.Equal(2, card.EnergyCost);

        player.PlayerCombatState.EndOfTurnCleanup();

        Assert.Null(card.TemporaryCostOverrideThisTurn);
        Assert.Equal(1, card.EnergyCost);

        DefendRegent ordered = AddToHand<DefendRegent>(player);
        ordered.AddEnergyCostUntilPlayed(-1);
        ordered.SetTemporaryCostOverrideThisTurn(3);
        Assert.Equal(3, ordered.LocalEnergyCost); // Later absolute cost overrides earlier reduction.
        ordered.AddEnergyCostUntilPlayed(-1);
        Assert.Equal(2, ordered.LocalEnergyCost); // Later relative cost applies to the absolute cost.
        ordered.AddEnergyCostThisCombat(2);
        Assert.Equal(4, ordered.LocalEnergyCost);
        ordered.SetTemporaryCostOverrideThisCombat(4);
        ordered.AddEnergyCostUntilPlayed(-1);
        Assert.Equal(3, ordered.LocalEnergyCost);

        StrikeRegent attack = AddToHand<StrikeRegent>(player);
        await PowerCmd.Apply<TangledPower>(room.Engine.State, player.Creature, 1m,
            player.Creature, null);
        Assert.Equal(1, attack.LocalEnergyCost);
        Assert.Equal(2, attack.EnergyCost); // Local query excludes Tangled's global Hook.
    }

    [Fact]
    public async Task EndOfTurnCleanup_ClearsOverride_AndRestoresUnplayedFreeUntilPlayed()
    {
        (Player player, _) = await CreateCombatAsync("override-cleanup");
        DefendRegent card = AddToHand<DefendRegent>(player);
        card.MakeTemporaryFreeThisTurn();
        card.MakeFreeUntilPlayed();
        card.SetTemporaryCostOverrideThisTurn(3);

        player.PlayerCombatState!.EndOfTurnCleanup();

        Assert.Null(card.TemporaryCostOverrideThisTurn);
        Assert.False(card.TemporaryFreeThisTurn);
        Assert.True(card.TemporaryFreeUntilPlayed);
        Assert.Equal(0, card.EnergyCost);
    }

    [Fact]
    public void MutableClone_ClearsEveryTransientCostStateWithoutMutatingTheSource()
    {
        var source = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        source.MakeTemporaryFreeThisTurn();
        source.MakeFreeUntilPlayed();
        source.SetTemporaryCostOverrideThisTurn(3);

        var clone = (DefendRegent)source.MutableClone();

        Assert.True(source.TemporaryFreeThisTurn);
        Assert.True(source.TemporaryFreeUntilPlayed);
        Assert.Equal(3, source.TemporaryCostOverrideThisTurn);
        Assert.False(clone.TemporaryFreeThisTurn);
        Assert.False(clone.TemporaryFreeUntilPlayed);
        Assert.Null(clone.TemporaryCostOverrideThisTurn);
        Assert.Equal(1, clone.EnergyCost);
    }

    [Fact]
    public void SetTemporaryCostOverrideThisTurn_NegativeCost_ThrowsWithoutChangingCost()
    {
        var card = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => card.SetTemporaryCostOverrideThisTurn(-1));

        Assert.Null(card.TemporaryCostOverrideThisTurn);
        Assert.Equal(1, card.EnergyCost);
    }

    [Fact]
    public void CombatEnergyCosts_IsDeterministicAndDoesNotPerturbExistingStreams()
    {
        var subject = new RunRngSet("combat-energy-costs");
        var untouched = new RunRngSet("combat-energy-costs");
        var expectedEnergyCosts = new Rng(subject.Seed, "combat_energy_costs");

        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(expectedEnergyCosts.NextInt(4), subject.CombatEnergyCosts.NextInt(4));
        }

        Assert.Equal(untouched.Shuffle.NextInt(1000), subject.Shuffle.NextInt(1000));
        Assert.Equal(
            untouched.CombatCardGeneration.NextInt(1000),
            subject.CombatCardGeneration.NextInt(1000));
        Assert.Equal(untouched.CombatTargets.NextInt(1000), subject.CombatTargets.NextInt(1000));
        Assert.Equal(untouched.MonsterAi.NextInt(1000), subject.MonsterAi.NextInt(1000));
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
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

internal sealed class TemporaryCostReplayCard : CardModel
{
    private List<int> _observedEnergyCosts = new();

    public IReadOnlyList<int> ObservedEnergyCosts => _observedEnergyCosts;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        _observedEnergyCosts.Add(cardPlay.Resources.EnergyValue);
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _observedEnergyCosts = new List<int>();
    }
}

internal sealed class TemporaryFixedStarCostCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override int CanonicalStarCost => 2;
}

internal sealed class ThrowingHandDepartureCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    public override Task AfterHandEmptied(Player player) =>
        throw new InvalidOperationException("hand-departure failure");
}
