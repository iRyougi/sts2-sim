using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Bellows : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    // 游戏 Bellows.AfterPlayerTurnStart：抽完手牌后、AfterSideTurnStart 之前；TurnNumber > 1 时不触发。
    public override Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner ||
            Owner.PlayerCombatState is not { TurnNumber: <= 1 })
        {
            return Task.CompletedTask;
        }

        foreach (CardModel card in Owner.PlayerCombatState.Hand.Cards)
        {
            // 游戏 Bellows 走 CardCmd.Upgrade：战斗正在结束时不升级（例如同一钩子里 FestivePopper 先清场）。
            Commands.CardCmd.Upgrade(card);
        }

        return Task.CompletedTask;
    }
}
