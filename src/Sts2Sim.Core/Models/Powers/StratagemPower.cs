using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Powers;

public sealed class StratagemPower : GeneratedPowerModel
{
    public override async Task AfterShuffle(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }
        PlayerCombatState combat = player.PlayerCombatState!;

        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromCombatPile(
            Owner.CombatState!,
            player,
            combat.DrawPile,
            Amount,
            Amount,
            this);
        foreach (CardModel card in selected)
        {
            CardPileCmd.Add(card, PileType.Hand);
        }
    }
}
