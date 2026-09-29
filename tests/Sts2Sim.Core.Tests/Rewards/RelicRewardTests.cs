using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

[Collection("ModelDb")]
public class RelicRewardTests : IDisposable
{
    public RelicRewardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight), typeof(Vajra), typeof(Circlet),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Populate_ReturnsVajraWhenCommonRolled_OrCircletWhenRolledRarityBucketIsEmpty()
    {
        Player player = CreatePlayer("relic-reward-populate");
        RelicRarity expectedRarity = RelicFactory.RollRarity(player.PlayerRng.Rewards.CloneExact());
        var reward = new RelicReward(player);

        reward.Populate((RunState)player.RunState);

        if (expectedRarity == RelicRarity.Common)
        {
            Assert.IsType<Vajra>(reward.Relic);
        }
        else
        {
            Assert.IsType<Circlet>(reward.Relic);
        }
    }

    [Fact]
    public async Task Take_ClonesRelicAndIsIdempotent()
    {
        Player player = CreatePlayer("relic-reward-take");
        RelicRarity expectedRarity = RelicFactory.RollRarity(player.PlayerRng.Rewards.CloneExact());
        var reward = new RelicReward(player);
        reward.Populate((RunState)player.RunState);

        Task first = reward.Take();
        Task retry = reward.Take();
        await Task.WhenAll(first, retry);

        RelicModel owned = expectedRarity == RelicRarity.Common
            ? Assert.Single(player.Relics.OfType<Vajra>())
            : Assert.Single(player.Relics.OfType<Circlet>());
        Assert.Same(first, retry);
        Assert.NotSame(reward.Relic, owned);
        Assert.Same(player, owned.Owner);
        Assert.True(reward.IsResolved);
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }

    [Fact]
    public async Task Populate_WhenBagIsExhausted_FallsBackToCirclet()
    {
        Player player = CreatePlayer("relic-reward-exhausted");
        Assert.IsType<Vajra>(player.RelicGrabBag.PullFromFront(Sts2Sim.Core.Entities.Relics.RelicRarity.Common));
        var reward = new RelicReward(player);

        reward.Populate((RunState)player.RunState);
        await reward.Take();

        Assert.IsType<Circlet>(reward.Relic);
        Assert.Contains(player.Relics, relic => relic is Circlet);
    }
}
