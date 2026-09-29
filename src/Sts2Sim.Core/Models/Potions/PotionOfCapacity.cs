using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class PotionOfCapacity : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return OrbCmd.AddSlots(
            target.Player ?? throw new InvalidOperationException("Target must be a player."), 2);
    }
}
