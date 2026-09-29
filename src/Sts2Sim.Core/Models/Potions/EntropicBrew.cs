using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Potions;

public sealed class EntropicBrew : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.AnyTime;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player player = target.Player ?? throw new InvalidOperationException("Entropic Brew requires a player target.");

        while (player.PotionSlots.Contains(null))
        {
            // The real potion deliberately uses the out-of-combat pool even when consumed during combat.
            PotionModel? potion = PotionFactory.CreateRandomOutOfCombat(
                player, player.RunState.Rng.CombatPotionGeneration);
            if (potion is null || !await PotionCmd.TryToProcure(potion, player))
            {
                break;
            }
        }
    }
}
