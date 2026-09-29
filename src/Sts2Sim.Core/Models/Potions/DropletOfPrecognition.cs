using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class DropletOfPrecognition : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player player = target.Player
            ?? throw new InvalidOperationException("Droplet of Precognition requires a player target.");
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(
            player.Creature.CombatState!, player, player.PlayerCombatState!.DrawPile.Cards,
            1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Hand);
        }
    }
}
