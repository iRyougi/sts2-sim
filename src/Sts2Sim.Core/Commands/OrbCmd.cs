using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Orbs;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Commands;

public static class OrbCmd
{
    public static Task AddSlots(Player player, int amount)
    {
        ICombatState combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("Player is not in combat.");
        if (combatState.IsOverOrEnding())
            return Task.CompletedTask;
        OrbQueue queue = player.PlayerCombatState!.OrbQueue;
        amount = Math.Min(OrbQueue.MaxCapacity - queue.Capacity, amount);
        queue.AddCapacity(amount);
        return Task.CompletedTask;
    }

    public static void RemoveSlots(Player player, int amount)
    {
        ICombatState combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("Player is not in combat.");
        if (combatState.IsOverOrEnding())
            return;
        OrbQueue queue = player.PlayerCombatState!.OrbQueue;
        amount = Math.Min(queue.Capacity, amount);
        queue.RemoveCapacity(amount);
    }

    public static Task Channel<T>(ICombatState combatState, Player player)
        where T : OrbModel =>
        Channel(combatState, ModelDb.Orb<T>().ToMutable(), player);

    public static async Task Channel(ICombatState combatState, OrbModel orb, Player player)
    {
        if (combatState.IsOverOrEnding())
            return;

        OrbQueue queue = player.PlayerCombatState!.OrbQueue;
        if (player.Character.BaseOrbSlotCount == 0 && queue.Capacity == 0)
            await AddSlots(player, 1);

        orb.AssertMutable();
        orb.Owner = player;
        if (queue.Orbs.Count >= queue.Capacity)
            await EvokeNext(combatState, player);

        if (await queue.TryEnqueue(orb))
        {
            if (combatState is CombatState state)
                state.SemanticHistory.RecordOrbChanneled(state, orb);
            await Hook.AfterOrbChanneled(combatState, player, orb);
        }
    }

    public static Task EvokeNext(ICombatState combatState, Player player, bool dequeue = true)
    {
        OrbQueue queue = player.PlayerCombatState!.OrbQueue;
        return queue.Orbs.Count == 0
            ? Task.CompletedTask
            : Evoke(combatState, player, queue.Orbs[0], dequeue);
    }

    public static Task EvokeLast(ICombatState combatState, Player player, bool dequeue = true)
    {
        OrbQueue queue = player.PlayerCombatState!.OrbQueue;
        return queue.Orbs.Count == 0
            ? Task.CompletedTask
            : Evoke(combatState, player, queue.Orbs[^1], dequeue);
    }

    private static async Task Evoke(
        ICombatState combatState,
        Player player,
        OrbModel orb,
        bool dequeue)
    {
        if (combatState.IsOverOrEnding())
            return;
        OrbQueue queue = player.PlayerCombatState!.OrbQueue;
        if (queue.Orbs.Count == 0)
            return;

        bool removed = dequeue && queue.Remove(orb);
        IReadOnlyList<Creature> targets = await orb.Evoke(combatState);
        if (player.Creature.CombatState is { } currentCombat)
        {
            await Hook.AfterOrbEvoked(currentCombat, orb, targets);
            if (removed)
                orb.RemoveInternal();
        }
    }

    public static async Task Passive(
        ICombatState combatState,
        OrbModel orb,
        Creature? target = null,
        bool countAffectedByHooks = false)
    {
        if (combatState.IsOverOrEnding())
            return;
        if (countAffectedByHooks)
            await orb.TriggerPassive(combatState, target);
        else
            await orb.Passive(combatState, target);
    }
}
