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

    // 游戏 ToastyMittens.AfterPlayerTurnStart：每个玩家回合抽完手牌后、AfterSideTurnStart 之前
    // （晚于它会排到 AfterSideTurnStart 里的 Vigor 类授予之后，能力列表顺序就与游戏不同）。
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
