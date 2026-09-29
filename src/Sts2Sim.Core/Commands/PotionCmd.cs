using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Commands;

/// <summary>Executes potion use and notifies run-level listeners.</summary>
public static class PotionCmd
{
    public static async Task Discard(PotionModel potion)
    {
        if (!potion.Owner.CanUseOrRemovePotions)
            throw new InvalidOperationException("Potions are locked by the current event.");
        await DiscardForEvent(potion);
    }

    /// <summary>Event effects discard their promised potion while manual use/removal is locked.</summary>
    public static async Task DiscardForEvent(PotionModel potion)
    {
        if (!potion.Owner.PotionSlots.Contains(potion))
            throw new InvalidOperationException("Potion is no longer in its owner's slots.");
        potion.Owner.RemovePotionInternal(potion);
        await Hook.AfterPotionDiscarded(potion.Owner.RunState, potion.Owner.Creature.CombatState, potion);
    }

    /// <summary>Queries the shared procurement veto without mutating slots or consuming RNG.</summary>
    public static bool CanProcure(PotionModel potion, Player player)
    {
        ArgumentNullException.ThrowIfNull(potion);
        ArgumentNullException.ThrowIfNull(player);
        return Hook.ShouldProcurePotion(
            player.RunState,
            player.Creature.CombatState,
            potion,
            player);
    }

    /// <summary>All gameplay potion acquisition routes through this gate; trusted setup/restore may
    /// still use <see cref="Player.AddPotionInternal"/> directly. History/UI remain omitted (#170).</summary>
    public static async Task<bool> TryToProcure(PotionModel potion, Player player)
    {
        ArgumentNullException.ThrowIfNull(potion);
        ArgumentNullException.ThrowIfNull(player);
        potion.AssertMutable();
        if (!CanProcure(potion, player))
            return false;
        if (potion.Owner is not null && !ReferenceEquals(potion.Owner, player))
        {
            throw new InvalidOperationException("Potion is owned by another player.");
        }
        if (player.PotionSlots.Contains(potion))
        {
            throw new InvalidOperationException("Potion is already in a slot.");
        }
        if (!player.PotionSlots.Contains(null))
        {
            return false;
        }

        player.AddPotionInternal(potion);
        (player.Creature.CombatState as CombatState)?.PredictionSink?.PotionProcured(player, potion);
        await Hook.AfterPotionProcured(player.RunState, player.Creature.CombatState, potion);
        return true;
    }

    /// <summary>偏离 #72：仅省略投掷 VFX；效果深度与用药后空手检查保留。</summary>
    public static Task Use(PotionModel potion, Player player, Creature? target) =>
        UseInternal(potion, player, target, isAutomatic: false);

    /// <summary>Read-only manual-use gate for the production potion path and replay diagnostics.</summary>
    public static bool CanUseManually(ICombatState combatState, Player player, PotionModel potion, Creature? target)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(potion);
        return ReferenceEquals(potion.Owner, player) &&
            player.PotionSlots.Contains(potion) &&
            potion.Usage != PotionUsage.Automatic &&
            player.CanUseOrRemovePotions &&
            potion.PassesCustomUsabilityCheck &&
            (potion.Usage != PotionUsage.CombatOnly || player.Creature.CombatState is not null) &&
            IsValidTarget(potion, player, target, combatState);
    }

    internal static Task UseAutomatic(PotionModel potion, Creature target)
    {
        if (potion.Usage != PotionUsage.Automatic)
        {
            throw new InvalidOperationException("Only automatic potions can use the automatic path.");
        }

        return UseInternal(potion, potion.Owner, target, isAutomatic: true);
    }

    private static async Task UseInternal(
        PotionModel potion,
        Player player,
        Creature? target,
        bool isAutomatic)
    {
        ICombatState? combatState = player.Creature.CombatState;
        if (combatState is CombatState concreteState &&
            concreteState.Engine is { } engine)
        {
            await engine.ExecuteCardActionBoundaryAsync(async () =>
            {
                await UseInternalCore(potion, player, target, isAutomatic);
                return true;
            });
            return;
        }

        await UseInternalCore(potion, player, target, isAutomatic);
    }

    private static async Task UseInternalCore(
        PotionModel potion,
        Player player,
        Creature? target,
        bool isAutomatic)
    {
        if (!ReferenceEquals(potion.Owner, player) || !player.PotionSlots.Contains(potion))
        {
            throw new InvalidOperationException("Potion is not in this player's potion slots.");
        }

        if (potion.Usage == PotionUsage.Automatic && !isAutomatic)
        {
            throw new InvalidOperationException("Automatic potions cannot be used manually.");
        }

        if ((!isAutomatic && !player.CanUseOrRemovePotions) || !potion.PassesCustomUsabilityCheck)
            throw new InvalidOperationException("Potion cannot be used in the current state.");

        ICombatState? combatState = player.Creature.CombatState;
        using IDisposable? rngScope = (combatState as CombatState)?.BeginPotionRngScope(potion);
        if (potion.Usage == PotionUsage.CombatOnly && combatState == null)
        {
            throw new InvalidOperationException("This potion can only be used in combat.");
        }

        ValidateTarget(potion, player, target, combatState);
        if (!isAutomatic && combatState is not null && !CanUseManually(combatState, player, potion, target))
            throw new InvalidOperationException("Potion cannot be used in the current state.");
        ICombatObserver? observer = (combatState as CombatState)?.Observer;
        try
        {
            observer?.PotionUseStarted(potion, target);
            player.RemovePotionInternal(potion);
            await Hook.BeforePotionUsed(player.RunState, combatState, potion, target);
            PlayerCombatState? effectState = player.PlayerCombatState;
            if (effectState is not null) effectState.CardOrPotionEffectDepth++;
            try
            {
                await potion.UseInternal(target);
            }
            finally
            {
                if (effectState is not null) effectState.CardOrPotionEffectDepth--;
            }
            await Hook.AfterPotionUsed(player.RunState, potion, player);
            if (combatState is not null) await CardPileCmd.CheckForEmptyHand(combatState, player);
            observer?.PotionUseFinished(potion, target);
        }
        catch
        {
            TryAbortPotionUse(observer, potion, target);
            throw;
        }
    }

    private static void TryAbortPotionUse(
        ICombatObserver? observer,
        PotionModel potion,
        Creature? target)
    {
        try
        {
            observer?.PotionUseAborted(potion, target);
        }
        catch
        {
            // Preserve the original core or observer callback exception.
        }
    }

    private static void ValidateTarget(PotionModel potion, Player player, Creature? target, ICombatState? combatState)
    {
        if (!IsValidTarget(potion, player, target, combatState))
            throw new InvalidOperationException("Potion target is not valid for the current combat.");
    }

    private static bool IsValidTarget(PotionModel potion, Player player, Creature? target, ICombatState? combatState)
    {
        bool targetInCombat = target is not null &&
            combatState is not null &&
            ReferenceEquals(target.CombatState, combatState) &&
            combatState.ContainsCreature(target);
        bool targetIsRunPlayerOutsideCombat = target?.Player is Player targetPlayer &&
            combatState is null &&
            potion.Usage == PotionUsage.AnyTime &&
            ReferenceEquals(targetPlayer.RunState, player.RunState) &&
            player.RunState.Players.Contains(targetPlayer);

        return potion.TargetType switch
        {
            TargetType.None or TargetType.AllEnemies or TargetType.RandomEnemy or
                TargetType.AllAllies or TargetType.TargetedNoCreature => target is null,
            TargetType.Self => ReferenceEquals(target, player.Creature),
            TargetType.AnyPlayer => (targetInCombat && target!.IsPlayer) || targetIsRunPlayerOutsideCombat,
            TargetType.AnyAlly => targetInCombat && target!.Side == player.Creature.Side,
            TargetType.AnyEnemy => targetInCombat && target!.Side != player.Creature.Side && target.IsAlive && target.IsHittable,
            TargetType.Osty => targetInCombat,
            _ => false,
        };
    }
}
