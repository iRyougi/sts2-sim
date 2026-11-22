using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class VexingPuzzlebox : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    // 游戏 VexingPuzzlebox.AfterPlayerTurnStart：抽完手牌后、AfterSideTurnStart 之前，仅 TurnNumber == 1。
    public override async Task AfterPlayerTurnStart(Player player)
    {
        // 该条的后半段（"CharacterModel 尚无 CardPool 映射且当前只启用 Regent"）已于 2026-09-08
        // 销案（偏离 #303）：现按权威源码走 Owner.Character.CardPool + CardFactory.GetDistinctForCombat。
        if (player != Owner ||
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        CardModel? generated = CardFactory.GetDistinctForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(
                Owner.UnlockState,
                isMultiplayer: Owner.RunState.Players.Count > 1),
            1,
            Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (generated is null)
        {
            return;
        }

        generated.SetToFreeThisTurn();     // 游戏 VexingPuzzlebox 调 SetToFreeThisTurn
        await CardPileCmd.Generate(Owner.Creature.CombatState!, generated, PileType.Hand);
    }
}
