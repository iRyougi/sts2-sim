using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class BottledPotential : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player player = target.Player
            ?? throw new InvalidOperationException("Bottled Potential requires a player target.");
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Draw);
        }

        await CardPileCmd.Shuffle(target.CombatState!, player);
        await CardPileCmd.Draw(target.CombatState!, 5, player, fromHandDraw: false);
    }
}
