using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rewards;

/// <summary>Base class for rewards offered to a player. 偏离 #75：省略真实游戏的 <c>RewardsSetIndex</c>
/// 排序字段——本计划奖励种类少（4 种），生成顺序在 <see cref="Sts2Sim.Core.Rewards.RewardsSet"/> 里
/// 硬编码即可。</summary>
public abstract class Reward
{
    public Player Player { get; }

    public bool IsResolved { get; protected set; }

    protected Reward(Player player)
    {
        Player = player;
    }

    public abstract void Populate(IRunState runState);
}
