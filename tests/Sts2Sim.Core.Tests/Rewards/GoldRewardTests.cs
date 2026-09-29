using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

public class GoldRewardTests : IDisposable
{
    public GoldRewardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Populate_BossRoom_AlwaysYieldsExactlyOneHundred()
    {
        var runState = new RunState("gold-reward-boss", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var reward = new GoldReward(RoomType.Boss, player);
        reward.Populate(runState);

        Assert.Equal(100, reward.Amount);
    }

    [Fact]
    public void Populate_EqualBossBoundsConsumesOneRewardDrawButZeroProportionDoesNot()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
        var run = new RunState("8F9CPYQ6QYEN", ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        // Upstream range-based GoldReward.Populate calls NextInt even for 75..75.
        // F17's 75-gold payout must still advance the stream before its card candidates.
        var bossGold = new GoldReward(RoomType.Boss, player, 1f, 75, 75);
        bossGold.Populate(run);
        Assert.Equal(75, bossGold.Amount);
        Assert.Equal(1, player.PlayerRng.Rewards.Counter);

        // Preserve #310: the retained empty slot represents an upstream omitted reward.
        var omittedGold = new GoldReward(RoomType.Monster, player, 0f, 10, 20);
        omittedGold.Populate(run);
        Assert.Equal(0, omittedGold.Amount);
        Assert.Equal(1, player.PlayerRng.Rewards.Counter);

        // A fixed GoldReward is already populated on construction, but the native
        // combat reward set still calls Populate and consumes its equal-bound draw.
        var fixedGold = new GoldReward(60, player);
        Assert.Equal(60, fixedGold.Amount);
        fixedGold.Populate(run);
        Assert.Equal(60, fixedGold.Amount);
        Assert.Equal(2, player.PlayerRng.Rewards.Counter);
    }
    [Fact]
    public void Populate_MonsterRoom_YieldsAmountWithinTenToTwenty()
    {
        var runState = new RunState("gold-reward-monster", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var reward = new GoldReward(RoomType.Monster, player);
        reward.Populate(runState);

        Assert.InRange(reward.Amount, 10, 20);
    }

    [Fact]
    public async Task Take_AddsAmountToPlayerGold()
    {
        var runState = new RunState("gold-reward-take", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var reward = new GoldReward(RoomType.Elite, player);
        reward.Populate(runState);
        int before = player.Gold;

        await reward.Take();

        Assert.Equal(before + reward.Amount, player.Gold);
    }
}
