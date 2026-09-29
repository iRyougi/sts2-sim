using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Commands;

/// <summary>power 的施加/叠加/移除命令。逐字移植调用序（<c>MegaCrit.Sts2.Core.Commands.PowerCmd</c>）。</summary>
public static class PowerCmd
{
    public static async Task<T?> Apply<T>(ICombatState combatState, Creature target, decimal amount, Creature? applier, CardModel? cardSource) where T : PowerModel
    {
        return (T?)await Apply(combatState, typeof(T), target, amount, applier, cardSource);
    }

    public static async Task<PowerModel?> Apply(
        ICombatState combatState,
        Type powerType,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        ArgumentNullException.ThrowIfNull(powerType);
        if (!typeof(PowerModel).IsAssignableFrom(powerType))
        {
            throw new ArgumentException($"{powerType.Name} is not a PowerModel.", nameof(powerType));
        }
        // Source CanReceivePowers rejects detached targets. Explicit fake contexts remain
        // available to command-only tests; real combat must own the target instance.
        if (combatState is null || (combatState is CombatState &&
            !ReferenceEquals(target.CombatState, combatState)))
            return null;

        if (amount == 0m)
        {
            return null;
        }

        if (!Hook.ShouldAllowHitting(combatState, target))
        {
            return null;
        }

        PowerModel canonical = ModelDb.GetById<PowerModel>(ModelDb.GetId(powerType));
        PowerModel? existing = FindExistingInstanceForStacking(canonical, target, applier);
        if (existing is null)
        {
            var power = (PowerModel)canonical.MutableClone();
            await ApplyNew(combatState, power, target, amount, applier, cardSource);
            return target.Powers.Contains(power) ? power : null;
        }

        await ModifyAmount(combatState, existing, amount, applier, cardSource);
        return existing;
    }

    private static async Task ApplyNew(ICombatState combatState, PowerModel power, Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        power.Applier = applier;
        await Hook.BeforePowerAmountChanged(combatState, power, amount, target, applier, cardSource);
        decimal modifiedAmount = amount;
        if (applier != null)
        {
            modifiedAmount = Hook.ModifyPowerAmountGiven(combatState, power, applier, modifiedAmount, target, cardSource, out _);
        }
        modifiedAmount = Hook.ModifyPowerAmountReceived(
            combatState,
            power,
            target,
            modifiedAmount,
            applier,
            out IEnumerable<AbstractModel> receivedModifiers);
        await power.BeforeApplied(target, modifiedAmount, applier, cardSource);
        if (modifiedAmount != 0m)
        {
            power.ApplyInternal(target, modifiedAmount);
            if (target.Side == CombatSide.Player && power.Type == PowerType.Debuff)
            {
                power.SkipNextDurationTick = true;
            }
        }
        await Hook.AfterModifyingPowerAmountReceived(combatState, receivedModifiers, power);
        if (modifiedAmount != 0m)
        {
            await power.AfterApplied(applier, cardSource);
            await Hook.AfterPowerAmountChanged(combatState, power, modifiedAmount, applier, cardSource);
        }
    }

    public static async Task<int> ModifyAmount(ICombatState combatState, PowerModel power, decimal offset, Creature? applier, CardModel? cardSource)
    {
        // Native PowerCmd.ModifyAmount returns before hooks when the power owner has left combat.
        // Command-only fake contexts have no attached creatures, as in Apply above.
        if (combatState is null || (combatState is CombatState &&
            !ReferenceEquals(power.Owner.CombatState, combatState)))
            return 0;

        await Hook.BeforePowerAmountChanged(combatState, power, offset, power.Owner, applier, cardSource);
        decimal modifiedOffset = offset;
        if (applier != null)
        {
            modifiedOffset = Hook.ModifyPowerAmountGiven(combatState, power, applier, modifiedOffset, power.Owner, cardSource, out _);
        }
        modifiedOffset = Hook.ModifyPowerAmountReceived(
            combatState,
            power,
            power.Owner,
            modifiedOffset,
            applier,
            out IEnumerable<AbstractModel> receivedModifiers);
        int newAmount = power.Amount + (int)modifiedOffset;
        power.SetAmount(newAmount);
        await Hook.AfterModifyingPowerAmountReceived(combatState, receivedModifiers, power);
        if (modifiedOffset != 0m)
        {
            await Hook.AfterPowerAmountChanged(combatState, power, modifiedOffset, applier, cardSource);
        }
        if (power.ShouldRemoveDueToAmount())
        {
            await Remove(power);
        }
        return newAmount;
    }

    public static async Task Remove(PowerModel power)
    {
        power.AssertMutable();
        Creature owner = power.Owner;
        power.RemoveInternal();
        await power.AfterRemoved(owner);
    }

    /// <summary>按权威生命周期消费 SkipNextDurationTick，否则递减一层。</summary>
    public static Task TickDownDuration(ICombatState combatState, PowerModel power)
    {
        (combatState as CombatState)?.Observer?.PowerDurationTick(power);
        if (power.SkipNextDurationTick)
        {
            power.SkipNextDurationTick = false;
            return Task.CompletedTask;
        }

        return ModifyAmount(combatState, power, -1m, null, null);
    }

    private static PowerModel? FindExistingInstanceForStacking(PowerModel canonical, Creature target, Creature? applier)
    {
        return canonical.InstanceType switch
        {
            PowerInstanceType.Instanced => null,
            PowerInstanceType.InstancedPerApplier => target.Powers.FirstOrDefault(p => p.Id == canonical.Id && p.Applier == applier),
            _ => target.Powers.FirstOrDefault(p => p.Id == canonical.Id),
        };
    }
}
