using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ToastyMittens : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 原版挂在 AfterPlayerTurnStart（抽完手牌后、AfterSideTurnStart 之前），不是 AfterSideTurnStart：
    // 时机晚了会排到赤牛（AfterSideTurnStart 给活力）之后，能力列表的先后顺序就和原版不同
    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner) return;
        CardModel? card = (await CardSelectCmd.FromHand(Owner.Creature.CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards, 1, 1, this, cancelable: false)).SingleOrDefault();
        if (card is not null)
            await CardPileCmd.Exhaust(Owner.Creature.CombatState!, card);
        await PowerCmd.Apply<StrengthPower>(
            Owner.Creature.CombatState!, Owner.Creature, 1m, Owner.Creature, null);
    }
}
