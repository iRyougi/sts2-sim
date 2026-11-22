using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GamblingChip : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    // 游戏 GamblingChip.AfterPlayerTurnStart：抽完手牌后、AfterSideTurnStart 之前；TurnNumber <= 1 时触发。
    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner ||
            Owner.PlayerCombatState is not { TurnNumber: <= 1 })
        {
            return;
        }

        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            Owner.Creature.CombatState!, Owner, Owner.PlayerCombatState.Hand.Cards,
            0, Owner.PlayerCombatState.Hand.Cards.Count, this);
        await CardCmd.DiscardAndDraw(selected, selected.Count);
    }
}
