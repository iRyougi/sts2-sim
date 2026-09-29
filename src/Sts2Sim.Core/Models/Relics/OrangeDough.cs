using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Relics;

public sealed class OrangeDough : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) ||
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState!;
        List<CardModel> candidates = ColorlessCardPool.Instance.GetUnlockedCards(
                Owner.UnlockState, Owner.RunState.Players.Count > 1)
            .ToList();

        foreach (CardModel generated in CardFactory.GetDistinctForCombat(
            Owner, candidates, 2, combatState.RunState.Rng.CombatCardGeneration))
        {
            await CardPileCmd.Generate(combatState, generated, PileType.Hand);
        }
    }
}
