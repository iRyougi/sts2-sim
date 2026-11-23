using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Bellows : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    // 原版挂在 AfterPlayerTurnStart（抽完手牌后、AfterSideTurnStart 之前），同一时机的遗物先后与随机数消耗顺序才和原版一样
    public override Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner ||
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return Task.CompletedTask;
        }

        foreach (CardModel card in Owner.PlayerCombatState.Hand.Cards)
        {
            if (card.IsUpgradable)
            {
                card.Upgrade();
            }
        }

        return Task.CompletedTask;
    }
}
