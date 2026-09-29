using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class DistilledChaos : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player player = target.Player
            ?? throw new InvalidOperationException("Distilled Chaos requires a player target.");
        return AutoPlayCmd.FromTopOfDrawPile(target.CombatState!, player, 3);
    }
}
