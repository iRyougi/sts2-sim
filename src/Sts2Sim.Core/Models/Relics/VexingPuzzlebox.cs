using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class VexingPuzzlebox : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        // 偏离 #131：无独立 AfterPlayerTurnStart；用摸牌后的 AfterSideTurnStart 过滤到持有者首回合。
        // 该条的后半段（"CharacterModel 尚无 CardPool 映射且当前只启用 Regent"）已于 2026-09-08
        // 销案（偏离 #303）：现按权威源码走 Owner.Character.CardPool + CardFactory.GetDistinctForCombat。
        if (!participants.Contains(Owner.Creature) ||
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

        generated.MakeTemporaryFreeThisTurn();
        await CardPileCmd.Generate(Owner.Creature.CombatState!, generated, PileType.Hand);
    }
}
