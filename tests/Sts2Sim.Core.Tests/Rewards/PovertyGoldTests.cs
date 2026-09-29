using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

/// <summary>
/// A3 Poverty: combat gold is multiplied by 0.75.
/// </summary>
[Collection("ModelDb")]
public sealed class PovertyGoldTests : IDisposable
{
    public PovertyGoldTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(RoomType.Monster, 0, 10, 20)]
    [InlineData(RoomType.Monster, 3, 7, 15)]
    [InlineData(RoomType.Elite, 0, 35, 45)]
    [InlineData(RoomType.Elite, 3, 26, 33)]
    [InlineData(RoomType.Boss, 0, 100, 100)]
    [InlineData(RoomType.Boss, 3, 75, 75)]
    [InlineData(RoomType.Elite, 10, 26, 33)]
    public void GoldReward_AppliesPovertyToBothEndsOfTheRange(RoomType roomType, int ascensionLevel, int min, int max)
    {
        (RunState run, Player player) = CreateRun("poverty-gold", ascensionLevel);
        var reward = new GoldReward(roomType, player);
        reward.Populate(run);
        Assert.InRange(reward.Amount, min, max);
    }

    [Theory]
    [InlineData(RoomType.Monster)]
    [InlineData(RoomType.Elite)]
    [InlineData(RoomType.Boss)]
    public void GoldReward_ConsumesTheSameRewardRngAtA0AndA10(RoomType roomType)
    {
        (RunState a0Run, Player a0) = CreateRun("poverty-rng", 0);
        (RunState a10Run, Player a10) = CreateRun("poverty-rng", 10);
        int a0Before = a0.PlayerRng.Rewards.Counter;
        int a10Before = a10.PlayerRng.Rewards.Counter;
        new GoldReward(roomType, a0).Populate(a0Run);
        new GoldReward(roomType, a10).Populate(a10Run);
        Assert.Equal(a0.PlayerRng.Rewards.Counter - a0Before, a10.PlayerRng.Rewards.Counter - a10Before);
    }

    [Fact]
    public void FixedAmountGoldReward_IgnoresPoverty()
    {
        (RunState run, Player player) = CreateRun("poverty-fixed", 10);
        var reward = new GoldReward(300, player);
        reward.Populate(run);
        Assert.Equal(300, reward.Amount);
    }

    private static (RunState, Player) CreateRun(string seed, int ascensionLevel)
    {
        var run = new RunState(seed, new Overgrowth(), ascensionLevel);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }
}
