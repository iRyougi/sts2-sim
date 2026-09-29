using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class RelicGrabBagSyncTests : IDisposable
{
    public RelicGrabBagSyncTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("reward")]
    [InlineData("shop")]
    [InlineData("obtain")]
    [InlineData("replace")]
    public async Task SeenOrObtainedRelics_AreRemovedFromBothRelevantBags(string source)
    {
        var run = new RunState("issue49-bag-sync", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        var removed = new List<RelicModel>();
        switch (source)
        {
            case "reward":
                var reward = new RelicReward(RelicRarity.Common, player);
                reward.Populate(run);
                removed.Add(Assert.IsAssignableFrom<RelicModel>(reward.Relic));
                break;
            case "shop":
                removed.AddRange(MerchantInventory.Generate(player).Relics.Select(entry => entry.Relic));
                break;
            case "obtain":
                await RelicCmd.Obtain(ModelDb.Relic<Anchor>(), player);
                removed.Add(player.Relics.Single(relic => relic is Anchor));
                break;
            case "replace":
                await RelicCmd.Replace(player.Relics.Single(), ModelDb.Relic<Anchor>());
                removed.Add(player.Relics.Single());
                break;
        }

        // Unpicked shop/reward candidates must already be absent; this is not an ownership filter.
        foreach (RelicGrabBag bag in new[] { player.RelicGrabBag, run.SharedRelicGrabBag! })
        {
            var remaining = new List<RelicModel>();
            foreach (RelicRarity rarity in new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop })
                while (bag.PullFromFront(rarity) is { } relic)
                    remaining.Add(relic);
            Assert.NotEmpty(remaining);
            foreach (RelicModel relic in removed)
                Assert.True(remaining.All(candidate => candidate.Id != relic.Id), $"source={source}, relic={relic.Id}");
        }
    }
}
