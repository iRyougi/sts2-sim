using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class IronClub : RelicModel
{
    private int _cardsPlayed;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner) return;
        // 原版 CardsPlayed 是跨战斗累计的存档属性，每打出第 4、8、12… 张时抽 1 张（取模，不清零）；
        // 移植快照把存档里的累计值原样放进 _cardsPlayed，原来“满 4 清零”的写法导入后第一张牌就会抽牌
        _cardsPlayed++;
        if (_cardsPlayed % 4 != 0 || Owner.Creature.CombatState is null) return;
        await CardPileCmd.Draw(Owner.Creature.CombatState, 1, Owner, fromHandDraw: false);
    }
    internal override void AppendCombatStateDescription(ref Combat.StateDescription.CombatStateDescriptionBuilder builder, Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_cardsPlayed % 4);
}
