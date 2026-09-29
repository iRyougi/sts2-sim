using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Kaleidoscope : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    /// <summary>偏离 #167（已解决）：真实游戏要求“全部角色卡池已解锁”才允许出现在 Neow 候选里；
    /// 本项目遵循“全解锁存档”原则，不移植任何 UnlockState 追踪，此门槛视为恒满足。</summary>
    public override bool IsAllowedAtNeow(IRunState runState) => true;

    /// <summary>偏离 #187 已销案（偏离 #307，2026-09-08）：Phase 3 已到，其他角色卡池已存在，
    /// 故按权威源码忠实实现——用 <c>RunState.Rng.Niche</c> 打乱"非本角色的卡池"取前 3 个，
    /// 每池各抽 1 张。此前是"从本角色卡池再抽 3 张"，且 RNG 流走的是 <c>PlayerRng.Rewards</c>。
    ///
    /// 当前五个可玩角色均已注册，每份奖励从四个他系池洗牌后取三个。
    /// #296 的 09a2 离线回放中 57 局 Niche 账本一致，但 53 局的卡牌候选内容仍不同（#303）；
    /// 抽取一致不能证明完整奖励等价。</summary>
    public override Task AfterObtained()
    {
        CardReward first = CreateChoice();
        CardReward second = CreateChoice();
        OfferRewards(RewardsSet.CreateCustom(
            Owner,
            card: first,
            extraRewards: new Reward[] { second }));
        return Task.CompletedTask;
    }

    private CardReward CreateChoice()
    {
        // 权威：Rng.Niche 打乱他系角色卡池，取前 3 个，每池抽 1 张。
        List<CardPoolModel> pools = Owner.UnlockState.CharacterCardPools
            .Where(pool => pool.GetType() != Owner.Character.CardPool.GetType())
            .ToList();
        Owner.RunState.Rng.Niche.Shuffle(pools);

        var options = new List<CardModel>();
        foreach (CardPoolModel pool in pools.Take(3))
        {
            CardModel? card = CardFactory.CreateForReward(
                Owner,
                optionCount: 1,
                new CardCreationOptions(
                    [pool],
                    CardCreationSource.Other,
                    CardRarityOddsType.RegularEncounter)).FirstOrDefault();
            if (card is not null)
            {
                options.Add(card);
            }
        }

        var reward = new CardReward(Owner, options);
        reward.Populate(Owner.RunState);
        return reward;
    }
}