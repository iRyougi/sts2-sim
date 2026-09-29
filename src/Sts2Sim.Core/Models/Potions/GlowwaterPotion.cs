using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class GlowwaterPotion : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Event;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Player is not { } player)
            throw new InvalidOperationException("GlowwaterPotion requires a player target.");
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray())
            await CardPileCmd.Exhaust(target.CombatState!, card);
        await CardPileCmd.Draw(target.CombatState!, 10, player, fromHandDraw: false);
    }
}
