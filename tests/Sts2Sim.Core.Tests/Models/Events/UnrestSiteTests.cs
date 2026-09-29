using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Events;

file sealed class LoseMaxHpProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public List<ValueProp> DamageProps { get; private set; } = new();
    public int DeathCount { get; private set; }

    public override Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        DamageProps.Add(props);
        return Task.CompletedTask;
    }

    public override Task AfterDeath(Creature target)
    {
        DeathCount++;
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        DamageProps = new List<ValueProp>(DamageProps);
    }
}

[Collection("ModelDb")]
public sealed class UnrestSiteTests : IDisposable
{
    public UnrestSiteTests()
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
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(Sts2Sim.Core.Models.Relics.Anchor),
            typeof(Sts2Sim.Core.Models.Relics.Akabeko),
            typeof(Sts2Sim.Core.Models.Relics.ArtOfWar),
            typeof(Sts2Sim.Core.Models.Relics.Circlet),
            typeof(PoorSleep),
            typeof(UnrestSite),
            typeof(LoseMaxHpProbeRelic),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();


    [Fact]
    public async Task IsAllowed_AllowsExactlySeventyPercentCurrentHpButRejectsOneHpAbove()
    {
        (RunState runState, Player player) = SetupPlayer("unrest-is-allowed");
        UnrestSite site = ModelDb.Event<UnrestSite>();

        Assert.False(site.IsAllowed(runState));

        await CreatureCmd.LoseMaxHp(runState, player.Creature, 5m, isFromCard: false);
        await CreatureCmd.LoseHp(runState, player.Creature, 21m, ValueProp.Unblockable);

        Assert.Equal(70, player.Creature.MaxHp);
        Assert.Equal(49, player.Creature.CurrentHp);
        Assert.True(site.IsAllowed(runState));

        await CreatureCmd.Heal(player.Creature, 1m);

        Assert.False(site.IsAllowed(runState));
    }
    [Fact]
    public async Task LoseMaxHp_FullHealthUsesUnblockableUnpoweredDamageThenReducesActualMaximum()
    {
        (RunState runState, Player player) = SetupPlayer("unrest-lose-max-full");
        await RelicCmd.Obtain(ModelDb.Relic<LoseMaxHpProbeRelic>(), player);
        var probe = Assert.IsType<LoseMaxHpProbeRelic>(player.Relics.Single(relic => relic is LoseMaxHpProbeRelic));
        int maxBefore = player.Creature.MaxHp;

        await CreatureCmd.LoseMaxHp(runState, player.Creature, 8m, isFromCard: false);

        Assert.Equal(maxBefore - 8, player.Creature.MaxHp);
        Assert.Equal(maxBefore - 8, player.Creature.CurrentHp);
        Assert.Equal(new[] { ValueProp.Unblockable | ValueProp.Unpowered }, probe.DamageProps);
        Assert.Equal(0, probe.DeathCount);
    }

    [Fact]
    public async Task LoseMaxHp_WhenCurrentHpIsBelowTheNewMaximum_DoesNotDealDamage()
    {
        (RunState runState, Player player) = SetupPlayer("unrest-lose-max-wounded");
        await CreatureCmd.LoseHp(runState, player.Creature, 20m, ValueProp.Unblockable);
        await RelicCmd.Obtain(ModelDb.Relic<LoseMaxHpProbeRelic>(), player);
        var probe = Assert.IsType<LoseMaxHpProbeRelic>(player.Relics.Single(relic => relic is LoseMaxHpProbeRelic));
        int currentBefore = player.Creature.CurrentHp;
        int maxBefore = player.Creature.MaxHp;

        await CreatureCmd.LoseMaxHp(runState, player.Creature, 8m, isFromCard: false);

        Assert.Equal(maxBefore - 8, player.Creature.MaxHp);
        Assert.Equal(currentBefore, player.Creature.CurrentHp);
        Assert.Empty(probe.DamageProps);
    }

    [Fact]
    public async Task LoseMaxHp_LethalLossRunsDamageAndDeathHooks_ThenClampsMaximumToOne()
    {
        (RunState runState, Player player) = SetupPlayer("unrest-lose-max-lethal");
        await RelicCmd.Obtain(ModelDb.Relic<LoseMaxHpProbeRelic>(), player);
        var probe = Assert.IsType<LoseMaxHpProbeRelic>(player.Relics.Single(relic => relic is LoseMaxHpProbeRelic));

        await CreatureCmd.LoseMaxHp(
            runState,
            player.Creature,
            player.Creature.MaxHp + 1m,
            isFromCard: false);

        Assert.Equal(1, player.Creature.MaxHp);
        Assert.Equal(0, player.Creature.CurrentHp);
        Assert.Equal(1, probe.DeathCount);
        Assert.Single(probe.DamageProps);
    }

    [Fact]
    public async Task LoseMaxHp_NegativeAmountThrowsBeforeMutation()
    {
        (RunState runState, Player player) = SetupPlayer("unrest-lose-max-negative");
        int currentBefore = player.Creature.CurrentHp;
        int maxBefore = player.Creature.MaxHp;

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreatureCmd.LoseMaxHp(runState, player.Creature, -1m, isFromCard: false));

        Assert.Equal(maxBefore, player.Creature.MaxHp);
        Assert.Equal(currentBefore, player.Creature.CurrentHp);
    }


    [Fact]
    public void InitialOptions_AreOfferedAtFullHealthWithoutAnEventLevelIsAllowedGate()
    {
        (_, Player player, UnrestSite ev) = SetupEvent("unrest-options");

        Assert.Equal(player.Creature.MaxHp, player.Creature.CurrentHp);
        Assert.Equal(new[] { "REST", "KILL" }, ev.CurrentOptions.Select(option => option.Key));
    }

    [Fact]
    public async Task Rest_HealsToFull_AndAddsOwnedPoorSleepToThePersistentDeck()
    {
        (RunState runState, Player player, UnrestSite ev) = SetupEvent("unrest-rest");
        await CreatureCmd.LoseHp(runState, player.Creature, 19m, ValueProp.Unblockable);
        int deckBefore = player.Deck.Cards.Count;

        await Choose(ev, "REST");

        Assert.Equal(player.Creature.MaxHp, player.Creature.CurrentHp);
        Assert.Equal(deckBefore + 1, player.Deck.Cards.Count);
        PoorSleep curse = Assert.Single(player.Deck.Cards.OfType<PoorSleep>());
        Assert.Same(player, curse.Owner);
        Assert.Same(player.Deck, curse.Pile);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Kill_LosesEightActualMaxHp_AndObtainsTheRolledRarityFrontRelic()
    {
        (_, Player player, UnrestSite ev) = SetupEvent("unrest-kill");
        int maxBefore = player.Creature.MaxHp;
        RelicRarity expectedRarity = RelicFactory.RollRarity(player.PlayerRng.Rewards.CloneExact());

        await Choose(ev, "KILL");

        Assert.Equal(maxBefore - 8, player.Creature.MaxHp);
        Assert.Equal(maxBefore - 8, player.Creature.CurrentHp);
        RelicModel obtained = Assert.Single(player.Relics, relic => relic.Rarity == expectedRarity);
        Assert.Same(player, obtained.Owner);
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, UnrestSite Event) SetupEvent(string seed)
    {
        (RunState runState, Player player) = SetupPlayer(seed);
        var ev = (UnrestSite)ModelDb.Event<UnrestSite>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(UnrestSite ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
    private static (RunState RunState, Player Player) SetupPlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
