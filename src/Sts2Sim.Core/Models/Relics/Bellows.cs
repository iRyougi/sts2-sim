using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Bellows : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        // 偏离 #123：无独立 AfterPlayerTurnStart；用摸牌后的 AfterSideTurnStart 过滤到持有者首回合。
        if (!participants.Contains(Owner.Creature) ||
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
