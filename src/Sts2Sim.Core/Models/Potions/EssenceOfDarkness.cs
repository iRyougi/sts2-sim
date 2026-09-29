using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Potions;

public sealed class EssenceOfDarkness : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var player = target.Player ?? throw new InvalidOperationException("Target must be a player.");
        int count = player.PlayerCombatState!.OrbQueue.Capacity;
        for (int i = 0; i < count; i++)
            await OrbCmd.Channel<DarkOrb>(target.CombatState!, player);
    }
}
