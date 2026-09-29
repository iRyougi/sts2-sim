using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class JeweledMask : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override Task BeforeHandDraw(Player player)
    {
        if (!ReferenceEquals(player, Owner) || Owner.PlayerCombatState is null || Owner.PlayerCombatState.TurnNumber > 1)
        {
            return Task.CompletedTask;
        }

        List<CardModel> powers = Owner.PlayerCombatState.DrawPile.Cards
            .Where(card => card.Type == CardType.Power)
            .ToList();
        if (powers.Count == 0) return Task.CompletedTask;
        List<CardModel> nonInnate = powers.Where(card => !card.HasKeyword(CardKeyword.Innate)).ToList();
        if (nonInnate.Count > 0) powers = nonInnate;
        CardModel? selected = Owner.RunState.Rng.CombatCardSelection.NextItem(powers);
        if (selected is not null)
        {
            selected.SetToFreeThisTurn();
            CardPileCmd.Add(selected, PileType.Hand);
        }

        return Task.CompletedTask;
    }
}
