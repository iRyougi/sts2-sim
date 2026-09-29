using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PowerCell : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants)
    {
        // The native <= 1 check runs before the turn increment. Only turn 0 is the first turn here.
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState!.TurnNumber != 0)
            return Task.CompletedTask;

        Flash();
        List<CardModel> candidates = Owner.PlayerCombatState.DrawPile.Cards
            .Where(card => !card.CostsXEnergy && card.LocalEnergyCost == 0).ToList();
        candidates.StableShuffle(Owner.RunState.Rng.CombatCardSelection);
        foreach (CardModel card in candidates.Take(2))
            CardPileCmd.Add(card, PileType.Hand);
        return Task.CompletedTask;
    }
}
