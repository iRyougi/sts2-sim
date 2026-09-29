using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class SilverCrucibleTests : IDisposable
{
    public SilverCrucibleTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void IsAllowed_OnlyInSinglePlayerAndDefaultTreasureHookAllowsGeneration()
    {
        RunState runState = CreateRun("silver-crucible-allowed", out Player owner);
        SilverCrucible canonical = ModelDb.Relic<SilverCrucible>();

        Assert.True(canonical.IsAllowed(runState));
        Assert.True(Hook.ShouldGenerateTreasure(runState, owner));

        Player foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(foreign);
        Assert.False(canonical.IsAllowed(runState));
    }

    [Fact]
    public async Task CardRewards_UpgradeEligibleOptionsAndConsumeExactlyOncePerOwnerReward()
    {
        RunState runState = CreateRun("silver-crucible-cards", out Player owner);
        Player foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(foreign);
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), owner);
        SilverCrucible relic = Assert.Single(owner.Relics.OfType<SilverCrucible>());
        StrikeRegent eligible = ModelDb.Card<StrikeRegent>();
        Greed ineligible = ModelDb.Card<Greed>();

        var mixed = new CardReward(owner, new CardModel[] { eligible, ineligible });
        mixed.Populate(runState);

        CardModel upgraded = mixed.Options[0];
        Assert.NotSame(eligible, upgraded);
        Assert.True(upgraded.IsUpgraded);
        Assert.Same(ineligible, mixed.Options[1]);
        Assert.False(eligible.IsUpgraded);
        Assert.Equal(1, relic.TimesUsed);

        var noneEligible = new CardReward(owner, new CardModel[] { ineligible });
        noneEligible.Populate(runState);
        Assert.Same(ineligible, Assert.Single(noneEligible.Options));
        Assert.Equal(2, relic.TimesUsed);

        var foreignReward = new CardReward(foreign, new CardModel[] { eligible });
        foreignReward.Populate(runState);
        Assert.Same(eligible, Assert.Single(foreignReward.Options));
        Assert.Equal(2, relic.TimesUsed);

        var third = new CardReward(owner, new CardModel[] { ineligible });
        third.Populate(runState);
        Assert.Equal(3, relic.TimesUsed);
        Assert.False(relic.IsUsedUp);

        var exhausted = new CardReward(owner, new CardModel[] { eligible });
        exhausted.Populate(runState);
        Assert.Same(eligible, Assert.Single(exhausted.Options));
        Assert.Equal(3, relic.TimesUsed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CardReward_ComposesSilverUpgradeAndSilkenGlamInEitherRelicOrder(bool silverFirst)
    {
        RunState runState = CreateRun($"silver-silken-{silverFirst}", out Player owner);
        if (silverFirst)
        {
            await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), owner);
            await RelicCmd.Obtain(ModelDb.Relic<SilkenTress>(), owner);
        }
        else
        {
            await RelicCmd.Obtain(ModelDb.Relic<SilkenTress>(), owner);
            await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), owner);
        }

        SilverCrucible silver = Assert.Single(owner.Relics.OfType<SilverCrucible>());
        SilkenTress silken = Assert.Single(owner.Relics.OfType<SilkenTress>());
        StrikeRegent original = ModelDb.Card<StrikeRegent>();
        var reward = new CardReward(owner, new CardModel[] { original });

        reward.Populate(runState);

        CardModel composed = Assert.Single(reward.Options);
        Assert.NotSame(original, composed);
        Assert.True(composed.IsUpgraded);
        Assert.IsType<Glam>(Assert.Single(composed.Enchantments));
        Assert.Equal(1, silver.TimesUsed);
        Assert.True(silken.IsUsedUp);
    }
    [Fact]
    public async Task TreasureRoomEnter_SuppressesFirstThenGrantsSecondAfterEntryCounterIncrements()
    {
        RunState runState = CreateRun("silver-crucible-treasure", out Player owner);
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), owner);
        SilverCrucible relic = Assert.Single(owner.Relics.OfType<SilverCrucible>());
        int goldBefore = owner.Gold;
        int relicCountBefore = owner.Relics.Count;

        await new TreasureRoom(40).Enter(runState);
        Assert.Equal(goldBefore, owner.Gold);
        Assert.Equal(relicCountBefore, owner.Relics.Count);
        Assert.Equal(1, relic.TreasureRoomsEntered);

        await new TreasureRoom(40).Enter(runState);
        Assert.Equal(goldBefore + 40, owner.Gold);
        Assert.Equal(relicCountBefore + 1, owner.Relics.Count);
        Assert.Equal(2, relic.TreasureRoomsEntered);

        await new TreasureRoom(40).Enter(runState);
        Assert.Equal(goldBefore + 80, owner.Gold);
        Assert.Equal(relicCountBefore + 2, owner.Relics.Count);
        Assert.Equal(3, relic.TreasureRoomsEntered);
    }

    [Fact]
    public async Task TreasureVeto_AffectsOnlyOwnerInMultiplayer()
    {
        RunState runState = CreateRun("silver-crucible-foreign", out Player owner);
        Player foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(foreign);
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), owner);
        int ownerGoldBefore = owner.Gold;
        int ownerRelicsBefore = owner.Relics.Count;
        int foreignGoldBefore = foreign.Gold;
        int foreignRelicsBefore = foreign.Relics.Count;

        await new TreasureRoom(25).Enter(runState);

        Assert.Equal(ownerGoldBefore, owner.Gold);
        Assert.Equal(ownerRelicsBefore, owner.Relics.Count);
        Assert.Equal(foreignGoldBefore + 25, foreign.Gold);
        Assert.Equal(foreignRelicsBefore + 1, foreign.Relics.Count);
    }

    [Fact]
    public async Task UsedUpRequiresThreeCardRewardsAndAtLeastOneTreasureEntry()
    {
        RunState runState = CreateRun("silver-crucible-used", out Player owner);
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), owner);
        SilverCrucible relic = Assert.Single(owner.Relics.OfType<SilverCrucible>());
        Greed ineligible = ModelDb.Card<Greed>();

        for (int i = 0; i < 3; i++)
        {
            new CardReward(owner, new CardModel[] { ineligible }).Populate(runState);
        }
        Assert.False(relic.IsUsedUp);

        await new TreasureRoom(0).Enter(runState);
        Assert.True(relic.IsUsedUp);
    }

    [Fact]
    public void Metadata_MatchesAncientCountersRelic()
    {
        SilverCrucible relic = ModelDb.Relic<SilverCrucible>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.Equal(0, relic.TimesUsed);
        Assert.Equal(0, relic.TreasureRoomsEntered);
        Assert.False(relic.IsUsedUp);
    }

    private static RunState CreateRun(string seed, out Player owner)
    {
        var runState = new RunState(seed, new Overgrowth());
        owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        return runState;
    }
}