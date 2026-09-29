using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GamblingChip : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        // 偏离 #124：无独立 AfterPlayerTurnStart；用摸牌后的 AfterSideTurnStart 过滤到持有者首回合。
        if (!participants.Contains(Owner.Creature) ||
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            Owner.Creature.CombatState!, Owner, Owner.PlayerCombatState.Hand.Cards,
            0, Owner.PlayerCombatState.Hand.Cards.Count, this);
        await CardCmd.DiscardAndDraw(selected, selected.Count);
    }
}
