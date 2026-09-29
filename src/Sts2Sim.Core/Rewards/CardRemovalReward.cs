using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rewards;

/// <summary>原版 <c>CardRemovalReward</c>：领取时从牌组选 1 张可删除的牌删掉（<c>RewardSynchronizer.DoUnsyncedCardRemoval</c>）。
/// 原版的选择界面可以取消（不删）：<see cref="RewardDecisionClassifier"/> 给出领取 / 放弃两个选项，领取后由决策源在牌组选牌时决定删哪张。
/// 原版 <c>RewardsSetIndex</c> 为 7，排在卡牌奖励之后，与额外奖励在 <see cref="RewardDecisionClassifier"/> 里的处理顺序一致。</summary>
public sealed class CardRemovalReward : TakeableReward
{
    public CardRemovalReward(Player player) : base(player)
    {
    }

    public override void Populate(IRunState runState)
    {
    }

    protected override async Task OnTake()
    {
        foreach (var card in await CardSelectCmd.FromDeckForRemoval(Player, 1))
            await CardPileCmd.RemoveFromDeck(Player, card);
    }
}
