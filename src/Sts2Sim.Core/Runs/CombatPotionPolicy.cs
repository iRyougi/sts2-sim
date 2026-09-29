using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Runs;

internal static class CombatPotionPolicy
{
    public static async Task<bool> TryUseFirstAvailableAsync(
        CombatEngine engine,
        Player player)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(player);
        if (!player.CanUseOrRemovePotions || !engine.IsInProgress || engine.State.HittableEnemies.Count == 0)
        {
            return false;
        }

        foreach (PotionModel? potion in player.PotionSlots)
        {
            if (potion is null || potion.Usage == PotionUsage.Automatic)
            {
                continue;
            }

            if (!TryChooseTarget(engine, player, potion.TargetType, out Creature? target))
            {
                continue;
            }

            await PotionCmd.Use(potion, player, target);
            return true;
        }

        return false;
    }

    private static bool TryChooseTarget(
        CombatEngine engine,
        Player player,
        TargetType targetType,
        out Creature? target)
    {
        target = targetType switch
        {
            TargetType.Self or TargetType.AnyPlayer or TargetType.AnyAlly => player.Creature,
            TargetType.AnyEnemy or TargetType.Osty => engine.State.HittableEnemies.FirstOrDefault(),
            _ => null,
        };

        return targetType switch
        {
            TargetType.AnyEnemy or TargetType.Osty => target is not null,
            TargetType.None or TargetType.Self or TargetType.AllEnemies or TargetType.RandomEnemy or
                TargetType.AnyPlayer or TargetType.AnyAlly or TargetType.AllAllies or
                TargetType.TargetedNoCreature => true,
            _ => false,
        };
    }
}
