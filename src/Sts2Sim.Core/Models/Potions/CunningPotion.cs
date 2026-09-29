using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>Adds 3 upgraded Shivs to the targeted player's hand. Silent-specific potion (Silent4 epoch).</summary>
public sealed class CunningPotion : PotionModel
{
    private const int ShivCount = 3;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player player = target.Player
            ?? throw new InvalidOperationException("Cunning Potion must target a player.");
        foreach (CardModel shiv in await Shiv.CreateInHand(
                     player, ShivCount, target.CombatState!, Owner))
        {
            CardCmd.Upgrade(shiv);
        }
    }
}
