using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>Choose any number of cards in the targeted player's hand and exhaust them in order.</summary>
public sealed class Ashwater : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Player is not { } player || target.CombatState is not { } combatState)
            throw new InvalidOperationException("Ashwater requires a player in combat.");
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            combatState, player, player.PlayerCombatState!.Hand.Cards,
            0, int.MaxValue, this);
        foreach (CardModel card in selected)
            await CardPileCmd.Exhaust(combatState, card);
    }
}
