using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class SilkenTressTests : IDisposable
{
    public SilkenTressTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AfterObtained_ClearsGoldWithoutUponPickupPreviewFlag()
    {
        (_, Player player) = CreateRun("silken-tress-gold");
        Assert.True(player.Gold > 0);

        await RelicCmd.Obtain(ModelDb.Relic<SilkenTress>(), player);

        SilkenTress relic = Assert.Single(player.Relics.OfType<SilkenTress>());
        Assert.Equal(0, player.Gold);
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasUponPickupEffect);
        Assert.False(relic.IsUsedUp);
    }

    [Fact]
    public async Task NextOwnerReward_ClonesAndGlamsEnchantableOptionsThenDisables()
    {
        (RunState runState, Player player) = CreateRun("silken-tress-owner");
        await RelicCmd.Obtain(ModelDb.Relic<SilkenTress>(), player);
        SilkenTress relic = Assert.Single(player.Relics.OfType<SilkenTress>());
        Peck enchantable = ModelDb.Card<Peck>();
        Greed ineligible = ModelDb.Card<Greed>();
        var reward = new CardReward(player, new CardModel[] { enchantable, ineligible });

        reward.Populate(runState);

        CardModel replacement = reward.Options[0];
        Assert.NotSame(enchantable, replacement);
        Assert.False(replacement.IsCanonical);
        Glam glam = Assert.IsType<Glam>(Assert.Single(replacement.Enchantments));
        Assert.Equal(1m, glam.Magnitude);
        Assert.Same(ineligible, reward.Options[1]);
        Assert.Empty(enchantable.Enchantments);
        Assert.Empty(ineligible.Enchantments);
        Assert.True(relic.IsUsedUp);

        var later = new CardReward(player, new CardModel[] { enchantable });
        later.Populate(runState);
        Assert.Same(enchantable, Assert.Single(later.Options));
    }

    [Fact]
    public async Task OwnerRewardWithNoEnchantableOptions_StillConsumesOneShotWithoutReplacement()
    {
        (RunState runState, Player player) = CreateRun("silken-tress-ineligible");
        await RelicCmd.Obtain(ModelDb.Relic<SilkenTress>(), player);
        SilkenTress relic = Assert.Single(player.Relics.OfType<SilkenTress>());
        Greed ineligible = ModelDb.Card<Greed>();
        var reward = new CardReward(player, new CardModel[] { ineligible });

        reward.Populate(runState);

        Assert.Same(ineligible, Assert.Single(reward.Options));
        Assert.True(relic.IsUsedUp);
    }

    [Fact]
    public async Task ForeignReward_DoesNotModifyOrConsumeBeforeOwnerReward()
    {
        var runState = new RunState("silken-tress-foreign", new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(foreign);
        await RelicCmd.Obtain(ModelDb.Relic<SilkenTress>(), owner);
        SilkenTress relic = Assert.Single(owner.Relics.OfType<SilkenTress>());
        Peck enchantable = ModelDb.Card<Peck>();
        var foreignReward = new CardReward(foreign, new CardModel[] { enchantable });

        foreignReward.Populate(runState);

        Assert.Same(enchantable, Assert.Single(foreignReward.Options));
        Assert.False(relic.IsUsedUp);

        var ownerReward = new CardReward(owner, new CardModel[] { enchantable });
        ownerReward.Populate(runState);
        Assert.NotSame(enchantable, Assert.Single(ownerReward.Options));
        Assert.True(relic.IsUsedUp);
    }

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
