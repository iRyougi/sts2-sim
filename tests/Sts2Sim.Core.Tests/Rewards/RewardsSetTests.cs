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

public class RewardsSetTests : IDisposable
{
    public RewardsSetTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight), typeof(Circlet),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void GenerateFor_EliteRoom_IncludesUnresolvedGoldCardAndRelicRewards()
    {
        (Player player, RunState runState) = CreatePlayer("rewards-set-elite");

        RewardsSet rewards = RewardsSet.GenerateFor(player, RoomType.Elite, runState);

        Assert.NotNull(rewards.Gold);
        Assert.NotNull(rewards.Card);
        Assert.NotNull(rewards.Relic);
        Assert.False(rewards.Gold.IsResolved);
        Assert.False(rewards.Card.IsResolved);
        Assert.False(rewards.Relic.IsResolved);
    }

    [Fact]
    public void GenerateFor_MonsterRoom_DoesNotIncludeRelicReward()
    {
        (Player player, RunState runState) = CreatePlayer("rewards-set-monster");

        RewardsSet rewards = RewardsSet.GenerateFor(player, RoomType.Monster, runState);

        Assert.Null(rewards.Relic);
    }

    [Fact]
    public void GenerateFor_BossRoom_YieldsOneHundredGoldWithoutRelicReward()
    {
        (Player player, RunState runState) = CreatePlayer("rewards-set-boss");

        RewardsSet rewards = RewardsSet.GenerateFor(player, RoomType.Boss, runState);

        Assert.Equal(100, rewards.Gold.Amount);
        Assert.Null(rewards.Relic);
    }

    private static (Player Player, RunState RunState) CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (player, runState);
    }
}
