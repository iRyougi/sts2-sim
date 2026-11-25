using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class RelicAcquisitionEligibilityTests : IDisposable
{
    public RelicAcquisitionEligibilityTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void RelicReward_SkipsIneligibleFront_AndRemovesItFromBag()
    {
        string seed = FindSeed(requireCommonRewardRoll: true);
        Player player = CreatePlayerAtFloor(seed, 41);
        var reward = new RelicReward(player);

        reward.Populate((RunState)player.RunState);

        Assert.IsNotType<AmethystAubergine>(reward.Relic);
        AssertRemovedFromBag(player);
    }

    [Fact]
    public async Task TreasureRoom_SkipsIneligibleFront_AndLeavesItInBag()
    {
        string seed = FindSeed(requireCommonRewardRoll: false);
        Player player = CreatePlayerAtFloor(seed, 41);
        var room = new TreasureRoom(goldAmount: 0);

        await room.EnterInternal((RunState)player.RunState);

        Assert.DoesNotContain(player.Relics, relic => relic is AmethystAubergine);
        Assert.IsType<AmethystAubergine>(player.RelicGrabBag.PullFromFront(RelicRarity.Common));
    }

    [Fact]
    public void RelicFactory_SkipsIneligibleFront_AndRemovesItFromBag()
    {
        string seed = FindSeed(requireCommonRewardRoll: true);
        Player player = CreatePlayerAtFloor(seed, 41);

        RelicModel relic = RelicFactory.PullNextRelicForRewardFromFront(player);

        Assert.IsNotType<AmethystAubergine>(relic);
        AssertRemovedFromBag(player);
    }

    /// <summary>#1120：原生 <c>RemoveDisallowedRelicsFromDeques</c> 在取用前永久移除不允许的遗物（MSA-04：旧断言锁定了"留在袋里"）。</summary>
    private static void AssertRemovedFromBag(Player player) =>
        Assert.DoesNotContain(
            player.RelicGrabBag.ExportForTransplant().Buckets.Values.SelectMany(ids => ids),
            id => id == ModelDb.Relic<AmethystAubergine>().Id.ToString());

    private static string FindSeed(bool requireCommonRewardRoll)
    {
        for (int index = 0; index < 10_000; index++)
        {
            string seed = $"relic-eligibility-{index}";
            Player player = CreatePlayerAtFloor(seed, 41);
            if (requireCommonRewardRoll &&
                RelicFactory.RollRarity(player.PlayerRng.Rewards.CloneExact()) != RelicRarity.Common)
            {
                continue;
            }

            if (player.RelicGrabBag.PullFromFront(RelicRarity.Common) is AmethystAubergine)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("Could not find a deterministic gated relic front draw.");
    }

    private static Player CreatePlayerAtFloor(string seed, int totalFloor)
    {
        var runState = new RunState(seed, new ActDefinition[] { new Overgrowth(), new Hive() });
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        for (int index = 0; index < totalFloor; index++)
        {
            runState.AddVisitedMapCoord(new Sts2Sim.Core.Map.MapCoord(index, 0));
        }

        return player;
    }
}
