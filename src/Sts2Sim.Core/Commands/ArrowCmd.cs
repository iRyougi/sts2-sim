using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Commands;

/// <summary>Fool 箭矢发射、穿刺、拔除与销毁命令（逐字移植 <c>MegaCrit.Sts2.Core.Commands.ArrowCmd</c> 语义）。</summary>
public static class ArrowCmd
{
    public static void GainArrowSlot(Player player) =>
        player.PlayerCombatState?.GainArrow();

    private static void RetrieveArrow(ArrowSlot arrowSlot)
    {
        if (arrowSlot.Status == ArrowStatus.Destroyed)
            throw new InvalidOperationException("Destroyed arrows cannot be retrieved.");
        if (arrowSlot.Status == ArrowStatus.Retrieved)
            throw new InvalidOperationException("This arrow has already been retrieved.");
        arrowSlot.Status = ArrowStatus.Retrieved;
        arrowSlot.DestroyAfterImpaled = false;
    }

    public static bool PlayerHasAnyArrows(Player player) =>
        TryGetNextArrowToShoot(player) is not null;

    public static int GetAvailableArrowCount(Player player) =>
        player.PlayerCombatState?.AvailableArrowCount ?? 0;

    private static ArrowSlot? TryGetNextArrowToShoot(Player player) =>
        player.PlayerCombatState?.Arrows.LastOrDefault(arrow => arrow.IsAvailable);

    public static bool CreatureIsImpaledByArrow(Creature creature) =>
        creature.Powers.OfType<ImpaledPower>().Any();

    public static bool CreatureIsImpaledByPlayersArrow(Creature creature, Player player) =>
        creature.Powers.OfType<ImpaledPower>().Any(power => ReferenceEquals(power.Applier, player.Creature));

    public static async Task<bool> ShootArrowAtTarget(
        Creature target,
        Player player,
        CardModel? card,
        Func<Task>? actionToPerform = null)
    {
        if (target.IsDead)
            return false;

        ArrowSlot? arrow = await TryShootArrow(player, card);
        if (arrow is not null)
        {
            await ImpaleTarget(target, arrow, actionToPerform, card);
        }
        return arrow is not null;
    }

    public static async Task<bool> ShootArrowAtTarget(
        Creature target,
        Player player,
        AttackCommand attackCommand)
    {
        if (target.IsDead)
            return false;

        ArrowSlot? arrow = await TryShootArrow(player, attackCommand.CardPlay?.Card ?? attackCommand.ModelSource as CardModel);
        if (arrow is not null)
        {
            await ImpaleTarget(target, arrow, attackCommand);
        }
        return arrow is not null;
    }

    public static async Task<ArrowSlot?> TryShootArrow(Player player, CardModel? source)
    {
        ICombatState? combatState = player.Creature.CombatState;
        if (combatState is null)
            return null;

        ArrowSlot? arrowSlot = TryGetNextArrowToShoot(player);
        if (arrowSlot is not null)
        {
            arrowSlot.Status = ArrowStatus.Away;
        }

        await Hook.AfterArrowShot(combatState, player, arrowSlot, source);
        return arrowSlot;
    }

    private static async Task ImpaleTarget(
        Creature target,
        ArrowSlot arrowSlot,
        AttackCommand attackCommand)
    {
        if (target.IsDead)
            return;

        ICombatState combatState = arrowSlot.Owner.Creature.CombatState!;
        CardModel? sourceCard = attackCommand.CardPlay?.Card ?? attackCommand.ModelSource as CardModel;
        await attackCommand.Execute();

        ImpaledPower? impaledInstance = target.Powers.OfType<ImpaledPower>()
            .FirstOrDefault(power => ReferenceEquals(power.Applier, arrowSlot.Owner.Creature));
        if (impaledInstance is null)
        {
            impaledInstance = (ImpaledPower)ModelDb.Power<ImpaledPower>().MutableClone();
            await PowerCmd.Apply(combatState, impaledInstance, target, 1m, arrowSlot.Owner.Creature, sourceCard);
        }

        impaledInstance.AddArrow(arrowSlot);
        await Hook.AfterArrowImpaled(combatState, impaledInstance, arrowSlot, null);

        if (arrowSlot.DestroyAfterImpaled)
        {
            await DestroyArrow(target, impaledInstance, arrowSlot);
        }
        else if (target.IsDead && impaledInstance.Arrows.Contains(arrowSlot))
        {
            await DislodgeArrow(combatState, impaledInstance, sourceCard);
        }
    }

    public static async Task ImpaleTarget(
        Creature target,
        ArrowSlot arrowSlot,
        Func<Task>? actionToPerform,
        CardModel? card)
    {
        if (target.IsDead)
            return;

        ICombatState combatState = arrowSlot.Owner.Creature.CombatState!;
        if (actionToPerform is not null)
        {
            await actionToPerform();
        }

        ImpaledPower? impaledInstance = target.Powers.OfType<ImpaledPower>()
            .FirstOrDefault(power => ReferenceEquals(power.Applier, arrowSlot.Owner.Creature));
        if (impaledInstance is null)
        {
            impaledInstance = (ImpaledPower)ModelDb.Power<ImpaledPower>().MutableClone();
            await PowerCmd.Apply(combatState, impaledInstance, target, 1m, arrowSlot.Owner.Creature, card);
        }

        impaledInstance.AddArrow(arrowSlot);
        await Hook.AfterArrowImpaled(combatState, impaledInstance, arrowSlot, card);

        if (arrowSlot.DestroyAfterImpaled)
        {
            await DestroyArrow(target, impaledInstance, arrowSlot);
        }
        else if (target.IsDead && impaledInstance.Arrows.Contains(arrowSlot))
        {
            await DislodgeArrow(combatState, impaledInstance, card);
        }
    }

    public static async Task DislodgeArrow(Creature target, Player player, CardModel? source)
    {
        if (!CreatureIsImpaledByArrow(target))
            throw new InvalidOperationException("This creature has no arrow to dislodge.");

        ImpaledPower power = target.Powers.OfType<ImpaledPower>()
            .First(impaledPower => ReferenceEquals(impaledPower.Applier, player.Creature));
        await DislodgeArrow(target.CombatState!, power, source);
        await power.RemoveIfNoArrows();
    }

    public static async Task DislodgeArrow(ICombatState combatState, ImpaledPower impaledArrow, CardModel? source)
    {
        ArrowSlot arrow = impaledArrow.RemoveFirstArrow();
        if (arrow.DestroyAfterImpaled)
        {
            arrow.Status = ArrowStatus.Destroyed;
            return;
        }

        await Hook.AfterArrowDislodged(combatState, impaledArrow, arrow, source);
        RetrieveArrow(arrow);
    }

    private static async Task DestroyArrow(Creature target, ImpaledPower impaledInstance, ArrowSlot arrow)
    {
        if (arrow.Status != ArrowStatus.Destroyed)
        {
            if (target.IsAlive)
            {
                await impaledInstance.ForceRemoveArrow(arrow);
            }
            arrow.Status = ArrowStatus.Destroyed;
        }
    }
}
