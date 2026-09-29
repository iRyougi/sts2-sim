using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LeadPaperweight : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterObtained()
    {
        // 偏离 #172 已销案（偏离 #305，2026-09-08）：权威就是一句
        // 使用工厂从无色卡池生成两个普通遭遇来源的奖励选项。
        // 此前手工滚稀有度 + 空桶回退整段都是本仓库自造的，理由是"本地扁平无色池可能缺少摇中的稀有度"
        // ——ColorlessCardPool 已存在，走池即可，稀有度与去重由工厂负责。
        IReadOnlyList<CardModel> options = CardFactory.CreateForReward(
            Owner,
            optionCount: 2,
            new CardCreationOptions(
                [ColorlessCardPool.Instance],
                CardCreationSource.Other,
                CardRarityOddsType.RegularEncounter));

        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, options, 0, 1, this, cancelable: true);
        if (selected.FirstOrDefault() is { } card)
        {
            await CardPileCmd.AddToDeck(card);
        }
    }
}