using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class TabletOfTruthTests : IDisposable
{
    public TabletOfTruthTests()
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
            typeof(LizardTail),
            typeof(TabletOfTruth),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Decipher_FiveTimes_UsesExactCostsAndUpgradesEveryEligibleCardOnFifthChoice()
    {
        (_, Player player, TabletOfTruth ev) = Setup("tablet-five");
        CardModel[] initiallyUpgradable = player.Deck.Cards.Where(card => card.IsUpgradable).ToArray();
        Assert.True(initiallyUpgradable.Length >= 2);
        Assert.Equal(new[] { "DECIPHER", "SMASH" }, ev.CurrentOptions.Select(option => option.Key));
        int[] expectedMaxHp = { 72, 66, 54, 30 };

        for (int index = 0; index < expectedMaxHp.Length; index++)
        {
            await Choose(ev, "DECIPHER");

            Assert.Equal(expectedMaxHp[index], player.Creature.MaxHp);
            Assert.Equal(index + 1, initiallyUpgradable.Count(card => card.IsUpgraded));
            Assert.Equal(new[] { "DECIPHER", "GIVE_UP" }, ev.CurrentOptions.Select(option => option.Key));
        }

        await Choose(ev, "DECIPHER");

        Assert.Equal(1, player.Creature.MaxHp);
        Assert.All(initiallyUpgradable, card => Assert.True(card.IsUpgraded));
        Assert.True(ev.IsFinished);
        Assert.Empty(ev.CurrentOptions);
    }

    [Fact]
    public async Task GiveUp_AfterDecipher_FinishesWithoutFurtherHpLossOrUpgrades()
    {
        (_, Player player, TabletOfTruth ev) = Setup("tablet-give-up");
        await Choose(ev, "DECIPHER");
        int maxHpBefore = player.Creature.MaxHp;
        int upgradedBefore = player.Deck.Cards.Count(card => card.IsUpgraded);

        await Choose(ev, "GIVE_UP");

        Assert.Equal(maxHpBefore, player.Creature.MaxHp);
        Assert.Equal(upgradedBefore, player.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Smash_HealsTwentyAndFinishes()
    {
        (RunState runState, Player player, TabletOfTruth ev) = Setup("tablet-smash");
        await CreatureCmd.LoseHp(
            runState,
            player.Creature,
            30m,
            ValueProp.Unblockable | ValueProp.Unpowered);
        int hpBefore = player.Creature.CurrentHp;

        await Choose(ev, "SMASH");

        Assert.Equal(hpBefore + 20, player.Creature.CurrentHp);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task FifthCost_IsCachedFromCurrentMaxHpAfterFourthDecipher()
    {
        (_, Player player, TabletOfTruth ev) = Setup("tablet-dynamic-fifth");
        await Choose(ev, "DECIPHER");
        await Choose(ev, "DECIPHER");
        await Choose(ev, "DECIPHER");
        await CreatureCmd.GainMaxHp(player.Creature, 10m);

        await Choose(ev, "DECIPHER");
        Assert.Equal(40, player.Creature.MaxHp);

        await CreatureCmd.GainMaxHp(player.Creature, 10m);
        await Choose(ev, "DECIPHER");

        Assert.Equal(11, player.Creature.MaxHp);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task CachedFifthCost_AtOrAboveCurrentMaxHp_UsesRunLevelFatalLifecycle()
    {
        (RunState runState, Player player, TabletOfTruth ev) = Setup("tablet-fatal");
        await PrepareCachedLethalFifth(ev, runState, player);
        int upgradedBefore = player.Deck.Cards.Count(card => card.IsUpgraded);
        Assert.Equal(4, upgradedBefore);

        await Choose(ev, "DECIPHER");

        Assert.Equal(upgradedBefore, player.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.Equal(1, player.Creature.MaxHp);
        Assert.Equal(0, player.Creature.CurrentHp);
        Assert.True(player.Creature.IsDead);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task CachedFifthCost_AtOrAboveCurrentMaxHp_AllowsRunLevelDeathPrevention()
    {
        (RunState runState, Player player, TabletOfTruth ev) = Setup("tablet-prevented");
        await RelicCmd.Obtain(ModelDb.Relic<LizardTail>(), player);
        await PrepareCachedLethalFifth(ev, runState, player);
        int upgradedBefore = player.Deck.Cards.Count(card => card.IsUpgraded);
        Assert.Equal(4, upgradedBefore);

        await Choose(ev, "DECIPHER");

        Assert.Equal(upgradedBefore, player.Deck.Cards.Count(card => card.IsUpgraded));
        LizardTail tail = Assert.Single(player.Relics.OfType<LizardTail>());
        Assert.Equal(1, player.Creature.MaxHp);
        Assert.Equal(1, player.Creature.CurrentHp);
        Assert.False(player.Creature.IsDead);
        Assert.True(tail.IsUsedUp);
        Assert.True(ev.IsFinished);
    }

    private static async Task PrepareCachedLethalFifth(
        TabletOfTruth ev,
        RunState runState,
        Player player)
    {
        for (int index = 0; index < 4; index++)
        {
            await Choose(ev, "DECIPHER");
        }

        Assert.Equal(30, player.Creature.MaxHp);
        await CreatureCmd.LoseMaxHp(runState, player.Creature, 25m, isFromCard: false);
        Assert.Equal(5, player.Creature.MaxHp);
    }

    private static (RunState RunState, Player Player, TabletOfTruth Event) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var ev = (TabletOfTruth)ModelDb.Event<TabletOfTruth>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(TabletOfTruth ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
