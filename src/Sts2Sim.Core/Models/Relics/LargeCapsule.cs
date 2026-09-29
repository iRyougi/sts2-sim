using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LargeCapsule : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterObtained()
    {
        for (int i = 0; i < 2; i++)
        {
            RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner);
            await RelicCmd.Obtain(relic, Owner);
        }

        await AddBasicCard(CardTag.Strike);
        await AddBasicCard(CardTag.Defend);
    }

    /// <summary>
    /// 逐字对照上游 LargeCapsule.GetStrikeForCharacter / GetDefendForCharacter：
    /// 从角色卡池中取第一张基础稀有度且包含目标标签的牌。
    /// 上游还针对测试模式启用且角色为 Deprived 设置了特例；本仓库没有 TestMode 与
    /// Deprived，不移植。
    ///
    /// 此前这里硬编码为 Regent 的 Strike/Defend，其他角色直接抛异常。Plan 08b-2 换成静默猎手后
    /// 该分支必然抛出，只是固定最左路线的既有夹具从未拿到过这个遗物；Plan 08b-6 Task 8 的随机
    /// 策略第三局就撞上了。两个角色卡池都包含基础牌，因此按上游判据直接取，不需要角色分支。
    /// </summary>
    private async Task AddBasicCard(CardTag tag)
    {
        CardModel prototype = Owner.Character.CardPool.AllCards
            .First(card => card.Rarity == CardRarity.Basic && card.Tags.Contains(tag));
        var card = (CardModel)prototype.MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(card);
    }
}
