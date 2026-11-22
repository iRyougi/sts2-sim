using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Crossbow : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return;
        }

        IEnumerable<CardModel> attacks = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.Players.Count > 1)
            .Where(card => card.Type == CardType.Attack);
        CardModel? generated = CardFactory.GetDistinctForCombat(
            Owner,
            attacks,
            1,
            Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (generated is null)
        {
            return;
        }

        generated.SetToFreeThisTurn();     // 游戏 Crossbow：SetToFreeThisTurn（本回合内或打出前免费；不覆盖 X 费用）
        await CardPileCmd.Generate(Owner.Creature.CombatState!, generated, PileType.Hand);
    }
}
